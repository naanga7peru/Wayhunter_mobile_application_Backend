:ON ERROR EXIT
-- APPROVAL-GATED: Do not execute before exact written authorization for D_PROCS.
-- Required sqlcmd -v M5AuthorizedPhase=D_PROCS; variable is an accidental-run fence,
-- NOT itself authorization. Run via -S "(localdb)\MSSQLLocalDB" -E -b -X.
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF N'$(M5AuthorizedPhase)' <> N'D_PROCS'
    THROW 57001, 'STOP: missing/wrong coordinator-approved phase', 1;
IF ISNULL(TRY_CONVERT(int,SERVERPROPERTY('IsLocalDB')),0) <> 1
    THROW 57002, 'STOP: not LocalDB', 1;
IF ORIGINAL_LOGIN() COLLATE Latin1_General_100_BIN2 <> N'UNICOMTIC\ut011148' COLLATE Latin1_General_100_BIN2
    THROW 57003, 'STOP: unexpected Windows login; coordinator must review', 1;
IF DB_NAME() <> N'Wayhunter_M5_Registry_IsolatedTest'
    THROW 57005, 'STOP: expected dedicated isolated database', 1;
IF DB_NAME() IN (N'master',N'model',N'msdb',N'tempdb')
    THROW 57006, 'STOP: system database', 1;
IF OBJECT_ID(N'dbo.M5RegistryFixtureManifest',N'U') IS NULL
    THROW 57007, 'STOP: missing M5 fixture manifest', 1;
IF NOT EXISTS (SELECT 1 FROM dbo.M5RegistryFixtureManifest
               WHERE RunId = CONVERT(uniqueidentifier,'F42D6B20-B250-4F93-AB07-0478C2FA8E85')
                 AND CreatedBy COLLATE Latin1_General_100_BIN2 = N'UNICOMTIC\ut011148' COLLATE Latin1_General_100_BIN2)
    THROW 57008, 'STOP: unrecognized fixture/owner', 1;

IF OBJECT_ID(N'dbo.M5Test_TXB_ConfirmConsumption',N'P') IS NOT NULL THROW 57021, 'Existing procedure; no overwrite', 1;
GO
CREATE PROCEDURE dbo.M5Test_TXB_ConfirmConsumption
  @OperationId uniqueidentifier,
  @GameId uniqueidentifier,
  @ClaimToken nvarchar(128),
  @ProviderReceiptId nvarchar(256),
  @AttemptId uniqueidentifier,
  @StepNumber int,
  @AuthenticatedPlayerUserId nvarchar(256),
  @CaptureSessionId nvarchar(256),
  @MediaProviderNamespace nvarchar(64),
  @CanonicalMediaId nvarchar(256),
  @FaultPoint nvarchar(32)=NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    IF @FaultPoint IS NOT NULL AND @FaultPoint<>N'AFTER_OPERATION_CONFIRM' THROW 58700,'Unsupported synthetic fault point',1;


    IF DB_NAME() <> N'Wayhunter_M5_Registry_IsolatedTest' OR ISNULL(TRY_CONVERT(int,SERVERPROPERTY('IsLocalDB')),0)<>1
        THROW 57101, 'STOP wrong database/server during procedure call', 1;

-- Caller MUST provide independently confirmed fake-provider receipt.
    -- This procedure never invokes M4 or infers consumption from a timeout.
    IF @ProviderReceiptId IS NULL OR LEN(LTRIM(RTRIM(@ProviderReceiptId)))=0
      OR @ClaimToken IS NULL OR LEN(LTRIM(RTRIM(@ClaimToken)))=0
      THROW 57121,'Confirmed fake receipt and claim token required',1;
    DECLARE @now datetimeoffset(7)=TODATETIMEOFFSET(SYSUTCDATETIME(),'+00:00');
    -- The receipt ID is *not* proof by itself. It must be pre-recorded by an
    -- approved synthetic fixture under its own restricted writer identity.
    -- No real M4 provider verification or production provider calls occur here.
    DECLARE @opState int,@claimState tinyint;
    DECLARE @attempt uniqueidentifier,@step int,@player nvarchar(256);
    BEGIN TRY
      SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
      BEGIN TRANSACTION;
      IF NOT EXISTS(SELECT 1 FROM dbo.Games WITH (UPDLOCK,HOLDLOCK) WHERE Id=@GameId)
        THROW 57122,'Game not found',1;
      -- Read immutable binding without taking update locks; then lock attempt/progress.
      SELECT @attempt=AttemptId,@step=StepNumber,@player=AuthenticatedPlayerUserId
        FROM dbo.GameProofSubmissionOperations WHERE SubmissionOperationId=@OperationId AND GameId=@GameId;
      IF @attempt IS NULL THROW 57127,'Operation missing; HOLD',1;
      IF NOT EXISTS(SELECT 1 FROM dbo.GameAttempts WITH (UPDLOCK,HOLDLOCK)
         WHERE Id=@attempt AND GameId=@GameId
           AND PlayerUserId COLLATE Latin1_General_100_BIN2=@player COLLATE Latin1_General_100_BIN2)
          THROW 57128,'Attempt owner mismatch',1;
      IF NOT EXISTS(SELECT 1 FROM dbo.GameAttemptProgress WITH (UPDLOCK,HOLDLOCK)
         WHERE AttemptId=@attempt AND GameId=@GameId AND CurrentStepNumber=@step
           AND PlayerUserId COLLATE Latin1_General_100_BIN2=@player COLLATE Latin1_General_100_BIN2)
          THROW 57129,'Progress changed; HOLD',1;
      SELECT @opState=State FROM dbo.GameProofSubmissionOperations WITH (UPDLOCK,HOLDLOCK)
        WHERE SubmissionOperationId=@OperationId AND GameId=@GameId
          AND ClaimToken COLLATE Latin1_General_100_BIN2 = @ClaimToken COLLATE Latin1_General_100_BIN2
          AND LeaseExpiresAtUtc>@now AND ProviderReceiptId IS NULL;
      -- Re-read the full immutable operation binding under lock.
      IF NOT EXISTS(SELECT 1 FROM dbo.GameProofSubmissionOperations WITH(UPDLOCK,HOLDLOCK)
          WHERE SubmissionOperationId=@OperationId AND GameId=@GameId
            AND AttemptId=@AttemptId AND StepNumber=@StepNumber
            AND AuthenticatedPlayerUserId COLLATE Latin1_General_100_BIN2=@AuthenticatedPlayerUserId COLLATE Latin1_General_100_BIN2
            AND CaptureSessionId COLLATE Latin1_General_100_BIN2=@CaptureSessionId COLLATE Latin1_General_100_BIN2
            AND MediaProviderNamespace=@MediaProviderNamespace AND CanonicalMediaId=@CanonicalMediaId)
        THROW 57130,'Binding mismatch: reject fake receipt',1;
      -- Game/attempt/progress locks already acquired: consult immutable fake receipt ledger.
      IF NOT EXISTS(SELECT 1 FROM dbo.M5TestSyntheticConsumedReceipts WITH(UPDLOCK,HOLDLOCK)
        WHERE ProviderReceiptId=@ProviderReceiptId COLLATE Latin1_General_100_BIN2
          AND SubmissionOperationId=@OperationId AND GameId=@GameId
          AND AttemptId=@AttemptId AND StepNumber=@StepNumber
          AND AuthenticatedPlayerUserId COLLATE Latin1_General_100_BIN2=@AuthenticatedPlayerUserId COLLATE Latin1_General_100_BIN2
          AND CaptureSessionId COLLATE Latin1_General_100_BIN2=@CaptureSessionId COLLATE Latin1_General_100_BIN2
          AND MediaProviderNamespace=@MediaProviderNamespace AND CanonicalMediaId=@CanonicalMediaId
          AND IsFakeConfirmedConsumed=1)
        THROW 57131,'No matching approved synthetic receipt: HOLD',1;
      IF @opState NOT IN (1,2) OR @opState IS NULL
        THROW 57123,'Lease/state/receipt inconsistent; reconcile not reconsume',1;
      SELECT @claimState=ClaimState FROM dbo.GameProofMediaClaims WITH (UPDLOCK,HOLDLOCK)
        WHERE SubmissionOperationId=@OperationId AND GameId=@GameId;
      IF NOT EXISTS(SELECT 1 FROM dbo.GameProofMediaClaims WITH(UPDLOCK,HOLDLOCK)
          WHERE SubmissionOperationId=@OperationId AND GameId=@GameId
            AND AttemptId=@AttemptId AND StepNumber=@StepNumber
            AND AuthenticatedPlayerUserId COLLATE Latin1_General_100_BIN2=@AuthenticatedPlayerUserId COLLATE Latin1_General_100_BIN2
            AND MediaProviderNamespace=@MediaProviderNamespace AND CanonicalMediaId=@CanonicalMediaId
            AND ClaimState=0)
        THROW 57132,'Registry immutable binding differs',1;
      IF @claimState<>0 OR @claimState IS NULL
        THROW 57124,'Registry state not reserved',1;
      UPDATE dbo.GameProofSubmissionOperations
        SET State=3,ProviderReceiptId=@ProviderReceiptId,ProviderConsumptionConfirmedAtUtc=@now,
            ClaimToken=NULL,LeaseExpiresAtUtc=@now,NextReconciliationAtUtc=@now,UpdatedAtUtc=@now
        WHERE SubmissionOperationId=@OperationId AND GameId=@GameId AND State=@opState;
      IF @@ROWCOUNT<>1 THROW 57125,'Operation confirmation count invalid',1;
      IF @FaultPoint=N'AFTER_OPERATION_CONFIRM'
          THROW 58702,'Synthetic TX-B fault between operation and claim UPDATE',1;
      UPDATE dbo.GameProofMediaClaims
        SET ClaimState=1,ProviderConsumedAtUtc=@now,UpdatedAtUtc=@now
        WHERE SubmissionOperationId=@OperationId AND GameId=@GameId AND ClaimState=0;
      IF @@ROWCOUNT<>1 THROW 57126,'Registry confirmation count invalid',1;
      COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
      IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
      THROW;
    END CATCH;
END;
GO
