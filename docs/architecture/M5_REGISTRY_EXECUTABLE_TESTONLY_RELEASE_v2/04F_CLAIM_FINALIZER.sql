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

IF OBJECT_ID(N'dbo.M5Test_ClaimFinalizer',N'P') IS NOT NULL
    THROW 57021,'Procedure exists; no overwrite',1;
GO
CREATE PROCEDURE dbo.M5Test_ClaimFinalizer
  @OperationId uniqueidentifier,
  @GameId uniqueidentifier,
  @FinalizerToken nvarchar(128),
  @LeaseSeconds int
AS
BEGIN
  SET NOCOUNT ON; SET XACT_ABORT ON;
  IF DB_NAME()<>N'Wayhunter_M5_Registry_IsolatedTest' OR ISNULL(TRY_CONVERT(int,SERVERPROPERTY('IsLocalDB')),0)<>1
      THROW 58601,'Wrong test database/server',1;
  IF NULLIF(LTRIM(RTRIM(@FinalizerToken)),N'') IS NULL
     OR LEN(@FinalizerToken)>128 OR @LeaseSeconds NOT BETWEEN 5 AND 600
     THROW 58602,'Invalid synthetic finalizer claim',1;
  DECLARE @now datetimeoffset(7)=TODATETIMEOFFSET(SYSUTCDATETIME(),'+00:00'),
          @attempt uniqueidentifier,@step int,@player nvarchar(256);
  BEGIN TRY
    SET TRANSACTION ISOLATION LEVEL SERIALIZABLE; BEGIN TRANSACTION;
    IF NOT EXISTS(SELECT 1 FROM dbo.Games WITH(UPDLOCK,HOLDLOCK) WHERE Id=@GameId)
        THROW 58603,'Game missing',1;
    SELECT @attempt=AttemptId,@step=StepNumber,@player=AuthenticatedPlayerUserId
      FROM dbo.GameProofSubmissionOperations WHERE SubmissionOperationId=@OperationId AND GameId=@GameId;
    IF @attempt IS NULL THROW 58604,'Operation missing',1;
    IF NOT EXISTS(SELECT 1 FROM dbo.GameAttempts WITH(UPDLOCK,HOLDLOCK)
      WHERE Id=@attempt AND GameId=@GameId AND PlayerUserId COLLATE Latin1_General_100_BIN2=@player COLLATE Latin1_General_100_BIN2)
      THROW 58605,'Attempt owner invalid',1;
    IF NOT EXISTS(SELECT 1 FROM dbo.GameAttemptProgress WITH(UPDLOCK,HOLDLOCK)
      WHERE AttemptId=@attempt AND GameId=@GameId AND CurrentStepNumber=@step AND Status=1)
      THROW 58606,'Progress no longer eligible',1;
    -- New finalizer only when no current holder. No lease-steal while active.
    UPDATE dbo.GameProofSubmissionOperations WITH(UPDLOCK)
      SET ClaimToken=@FinalizerToken,
          LeaseExpiresAtUtc=DATEADD(SECOND,@LeaseSeconds,@now),
          NextReconciliationAtUtc=DATEADD(SECOND,@LeaseSeconds,@now),UpdatedAtUtc=@now
      WHERE SubmissionOperationId=@OperationId AND GameId=@GameId AND State=3
        AND ProviderReceiptId IS NOT NULL AND EvidenceId IS NULL
        AND (ClaimToken IS NULL OR LeaseExpiresAtUtc<=@now);
    IF @@ROWCOUNT<>1 THROW 58607,'Current finalizer active or state invalid',1;
    IF NOT EXISTS(SELECT 1 FROM dbo.GameProofMediaClaims WITH(UPDLOCK,HOLDLOCK)
      WHERE SubmissionOperationId=@OperationId AND GameId=@GameId AND ClaimState=1)
      THROW 58608,'Confirmed registry state missing',1;
    COMMIT TRANSACTION;
  END TRY BEGIN CATCH
    IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
    THROW;
  END CATCH;
END;
GO
