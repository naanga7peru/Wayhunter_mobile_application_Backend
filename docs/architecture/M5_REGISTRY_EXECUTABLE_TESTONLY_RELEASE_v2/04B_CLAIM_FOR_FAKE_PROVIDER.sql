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

IF OBJECT_ID(N'dbo.M5Test_ClaimForFakeProvider',N'P') IS NOT NULL THROW 57021, 'Existing procedure; no overwrite', 1;
GO
CREATE PROCEDURE dbo.M5Test_ClaimForFakeProvider
  @OperationId uniqueidentifier,
  @GameId uniqueidentifier,
  @ClaimToken nvarchar(128),
  @LeaseMinutes int
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF DB_NAME() <> N'Wayhunter_M5_Registry_IsolatedTest' OR ISNULL(TRY_CONVERT(int,SERVERPROPERTY('IsLocalDB')),0)<>1
        THROW 57101, 'STOP wrong database/server during procedure call', 1;

IF @ClaimToken IS NULL OR LEN(LTRIM(RTRIM(@ClaimToken)))=0 OR @LeaseMinutes NOT BETWEEN 1 AND 10
       THROW 57112, 'Invalid synthetic lease claim', 1;
    DECLARE @now datetimeoffset(7)=TODATETIMEOFFSET(SYSUTCDATETIME(),'+00:00');
    BEGIN TRY
      SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
      BEGIN TRANSACTION;
      IF NOT EXISTS(SELECT 1 FROM dbo.Games WITH (UPDLOCK,HOLDLOCK) WHERE Id=@GameId)
          THROW 57113,'Game missing',1;
      DECLARE @attempt uniqueidentifier,@step int,@player nvarchar(256);
      SELECT @attempt=AttemptId,@step=StepNumber,@player=AuthenticatedPlayerUserId
        FROM dbo.GameProofSubmissionOperations WHERE SubmissionOperationId=@OperationId AND GameId=@GameId;
      IF @attempt IS NULL THROW 57117,'Operation missing',1;
      IF NOT EXISTS (SELECT 1 FROM dbo.GameAttempts WITH (UPDLOCK,HOLDLOCK)
        WHERE Id=@attempt AND GameId=@GameId
          AND PlayerUserId COLLATE Latin1_General_100_BIN2=@player COLLATE Latin1_General_100_BIN2)
        THROW 57118,'Attempt owner missing',1;
      IF NOT EXISTS (SELECT 1 FROM dbo.GameAttemptProgress WITH (UPDLOCK,HOLDLOCK)
        WHERE AttemptId=@attempt AND GameId=@GameId AND CurrentStepNumber=@step AND Status=1)
        THROW 57119,'Progress no longer eligible',1;
      IF NOT EXISTS (SELECT 1 FROM dbo.GameProofSubmissionOperations WITH (UPDLOCK,HOLDLOCK)
        WHERE SubmissionOperationId=@OperationId AND GameId=@GameId AND State=0)
          THROW 57114, 'Operation not pending; no additional claim',1;
      IF NOT EXISTS (SELECT 1 FROM dbo.GameProofMediaClaims WITH (UPDLOCK,HOLDLOCK)
        WHERE SubmissionOperationId=@OperationId AND ClaimState=0 AND ClaimOrigin=1)
          THROW 57115,'Registry not reserved',1;
      UPDATE dbo.GameProofSubmissionOperations
        SET State=1,ClaimToken=@ClaimToken,LeaseExpiresAtUtc=DATEADD(MINUTE,@LeaseMinutes,@now),
            NextReconciliationAtUtc=DATEADD(MINUTE,@LeaseMinutes,@now),UpdatedAtUtc=@now
        WHERE SubmissionOperationId=@OperationId AND GameId=@GameId AND State=0;
      IF @@ROWCOUNT<>1 THROW 57116,'Claim update failed',1;
      COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
      IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
      THROW;
    END CATCH;
END;
GO
