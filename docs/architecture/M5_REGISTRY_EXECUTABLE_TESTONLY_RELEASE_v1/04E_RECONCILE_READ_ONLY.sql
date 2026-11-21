:ON ERROR EXIT
-- APPROVAL-GATED: Do not execute before exact written authorization for D_PROCS.
-- Required sqlcmd -v M5AuthorizedPhase=D_PROCS; variable is an accidental-run fence,
-- NOT itself authorization. Run via -S "(localdb)\MSSQLLocalDB" -E -b -X.
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF N'$(M5AuthorizedPhase)' <> N'D_PROCS'
    THROW 57001, 'STOP: missing/wrong coordinator-approved phase', 1;
IF CONVERT(int,SERVERPROPERTY('IsLocalDB')) <> 1
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

IF OBJECT_ID(N'dbo.M5Test_ReconcileReadOnly',N'P') IS NOT NULL THROW 57021, 'Existing procedure; no overwrite', 1;
GO
CREATE PROCEDURE dbo.M5Test_ReconcileReadOnly
 @OperationId uniqueidentifier,
 @GameId uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF DB_NAME() <> N'Wayhunter_M5_Registry_IsolatedTest' OR CONVERT(int,SERVERPROPERTY('IsLocalDB'))<>1
        THROW 57101, 'STOP wrong database/server during procedure call', 1;

-- Read-only, always fail closed. Never invokes provider or mutates a row.
    DECLARE @opState int,@claimState tinyint,@opReceipt nvarchar(256),
            @opEvidence uniqueidentifier,@claimEvidence uniqueidentifier,
            @claimCount bigint,@reason nvarchar(64)=N'REVIEW_REQUIRED',@attempt uniqueidentifier,
            @step int;
    BEGIN TRY
      SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
      BEGIN TRANSACTION;
      IF NOT EXISTS(SELECT 1 FROM dbo.Games WITH (UPDLOCK,HOLDLOCK) WHERE Id=@GameId)
         THROW 57151,'Game not found',1;
      SELECT @attempt=AttemptId,@step=StepNumber
        FROM dbo.GameProofSubmissionOperations
        WHERE SubmissionOperationId=@OperationId AND GameId=@GameId;
      IF @attempt IS NOT NULL
      BEGIN
        -- Lock Attempt/Progress before Registry for consistent ordering.
        IF NOT EXISTS (SELECT 1 FROM dbo.GameAttempts WITH (UPDLOCK,HOLDLOCK) WHERE Id=@attempt AND GameId=@GameId)
            THROW 57152,'Attempt missing',1;
        IF NOT EXISTS (SELECT 1 FROM dbo.GameAttemptProgress WITH (UPDLOCK,HOLDLOCK) WHERE AttemptId=@attempt AND GameId=@GameId)
            THROW 57153,'Progress missing',1;
        SELECT @opState=State,@opReceipt=ProviderReceiptId,@opEvidence=EvidenceId
          FROM dbo.GameProofSubmissionOperations WITH (UPDLOCK,HOLDLOCK)
          WHERE SubmissionOperationId=@OperationId AND GameId=@GameId
            AND AttemptId=@attempt AND StepNumber=@step;
        IF @opState IS NULL THROW 57154,'Operation changed; HOLD',1;
        SELECT @claimCount=COUNT_BIG(*),@claimState=MAX(ClaimState),@claimEvidence=MAX(EvidenceId)
          FROM dbo.GameProofMediaClaims WITH (UPDLOCK,HOLDLOCK)
          WHERE SubmissionOperationId=@OperationId AND GameId=@GameId;
        IF @claimCount=1 AND @opState=0 AND @claimState=0 AND @opReceipt IS NULL
            SET @reason=N'CONSISTENT_RESERVED';
        ELSE IF @claimCount=1 AND @opState=3 AND @claimState=1 AND LEN(LTRIM(RTRIM(@opReceipt)))>0
            SET @reason=N'CONFIRMED_UNFINALIZED';
        ELSE IF @claimCount=1 AND @opState=4 AND @claimState=2
                 AND @opEvidence=@claimEvidence AND @opEvidence IS NOT NULL
                 AND EXISTS (SELECT 1 FROM dbo.GameProofEvidence e WHERE e.Id=@opEvidence
                  AND e.GameId=@GameId AND e.AttemptId=@attempt AND e.StepNumber=@step)
                 AND EXISTS (SELECT 1 FROM dbo.GameAttemptProgress p WHERE p.AttemptId=@attempt AND p.Status=2)
            SET @reason=N'COMPLETED';
      END;
      SELECT @reason AS ReconciliationState; -- No sensitive media/receipt or player data.
      COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
      IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
      THROW;
    END CATCH;
END;
GO
