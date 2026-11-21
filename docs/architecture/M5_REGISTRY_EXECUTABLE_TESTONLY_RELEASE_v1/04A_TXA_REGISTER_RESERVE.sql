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

IF OBJECT_ID(N'dbo.M5Test_TXA_RegisterReserve',N'P') IS NOT NULL THROW 57021, 'Existing procedure; no overwrite', 1;
GO
CREATE PROCEDURE dbo.M5Test_TXA_RegisterReserve
  @OperationId uniqueidentifier,
  @GameId uniqueidentifier,
  @AttemptId uniqueidentifier,
  @StepNumber int,
  @PlayerUserId nvarchar(256),
  @CaptureSessionId nvarchar(256),
  @MediaProviderNamespace nvarchar(64),
  @CanonicalMediaId nvarchar(256)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF DB_NAME() <> N'Wayhunter_M5_Registry_IsolatedTest' OR CONVERT(int,SERVERPROPERTY('IsLocalDB'))<>1
        THROW 57101, 'STOP wrong database/server during procedure call', 1;

IF @OperationId='00000000-0000-0000-0000-000000000000' OR @GameId='00000000-0000-0000-0000-000000000000'
       OR @AttemptId='00000000-0000-0000-0000-000000000000' OR @StepNumber < 1
       OR LEN(LTRIM(RTRIM(@PlayerUserId)))=0 OR LEN(LTRIM(RTRIM(@CaptureSessionId)))=0
       OR LEN(LTRIM(RTRIM(@MediaProviderNamespace)))=0 OR LEN(LTRIM(RTRIM(@CanonicalMediaId)))=0
       OR @PlayerUserId IS NULL OR @CaptureSessionId IS NULL OR @MediaProviderNamespace IS NULL OR @CanonicalMediaId IS NULL
       THROW 57102, 'Invalid trusted synthetic binding', 1;
    DECLARE @now datetimeoffset(7)=TODATETIMEOFFSET(SYSUTCDATETIME(),'+00:00');
    DECLARE @existing uniqueidentifier, @claimId uniqueidentifier;
    BEGIN TRY
      SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
      BEGIN TRANSACTION;
      -- Game FIRST: match existing SqlGameProofSubmissionRepository lock order.
      IF NOT EXISTS (SELECT 1 FROM dbo.Games WITH (UPDLOCK,HOLDLOCK) WHERE Id=@GameId)
        THROW 57103, 'Unknown game', 1;
      IF NOT EXISTS (SELECT 1 FROM dbo.GameAttempts WITH (UPDLOCK,HOLDLOCK)
                     WHERE Id=@AttemptId AND GameId=@GameId
                       AND PlayerUserId COLLATE Latin1_General_100_BIN2 = @PlayerUserId COLLATE Latin1_General_100_BIN2)
        THROW 57104, 'Attempt/player mismatch', 1;
      IF NOT EXISTS (SELECT 1 FROM dbo.GameAttemptProgress WITH (UPDLOCK,HOLDLOCK)
                     WHERE AttemptId=@AttemptId AND GameId=@GameId
                       AND PlayerUserId COLLATE Latin1_General_100_BIN2 = @PlayerUserId COLLATE Latin1_General_100_BIN2
                       )
        THROW 57105, 'Progress owner missing', 1;
      SELECT @existing=SubmissionOperationId FROM dbo.GameProofSubmissionOperations WITH (UPDLOCK,HOLDLOCK)
        WHERE SubmissionOperationId=@OperationId;
      IF @existing IS NOT NULL
      BEGIN
         IF NOT EXISTS (SELECT 1 FROM dbo.GameProofSubmissionOperations o
          JOIN dbo.GameProofMediaClaims c ON c.SubmissionOperationId=o.SubmissionOperationId
          WHERE o.SubmissionOperationId=@OperationId
            AND o.GameId=@GameId AND o.AttemptId=@AttemptId AND o.StepNumber=@StepNumber
            AND o.AuthenticatedPlayerUserId COLLATE Latin1_General_100_BIN2 = @PlayerUserId COLLATE Latin1_General_100_BIN2
            AND o.CaptureSessionId COLLATE Latin1_General_100_BIN2 = @CaptureSessionId COLLATE Latin1_General_100_BIN2
            AND o.MediaProviderNamespace COLLATE Latin1_General_100_BIN2 = @MediaProviderNamespace COLLATE Latin1_General_100_BIN2
            AND o.CanonicalMediaId COLLATE Latin1_General_100_BIN2 = @CanonicalMediaId COLLATE Latin1_General_100_BIN2
            AND c.GameId=@GameId AND c.AttemptId=@AttemptId AND c.StepNumber=@StepNumber
            AND c.ClaimOrigin=1 AND c.MediaProviderNamespace COLLATE Latin1_General_100_BIN2 = @MediaProviderNamespace COLLATE Latin1_General_100_BIN2
            AND c.CanonicalMediaId COLLATE Latin1_General_100_BIN2 = @CanonicalMediaId COLLATE Latin1_General_100_BIN2)
            THROW 57106, 'Inconsistent/conflicting durable binding; HOLD', 1;
         SELECT @claimId=MediaClaimId FROM dbo.GameProofMediaClaims WHERE SubmissionOperationId=@OperationId;
      END
      ELSE
      BEGIN
         IF NOT EXISTS (SELECT 1 FROM dbo.GameAttemptProgress WITH (UPDLOCK,HOLDLOCK)
           WHERE AttemptId=@AttemptId AND GameId=@GameId AND CurrentStepNumber=@StepNumber AND Status=1)
             THROW 57108,'New registration requires InProgress matching step',1;
         IF EXISTS (SELECT 1 FROM dbo.GameProofMediaClaims WITH (UPDLOCK,HOLDLOCK)
                   WHERE MediaProviderNamespace=@MediaProviderNamespace AND CanonicalMediaId=@CanonicalMediaId)
             THROW 57107, 'Media already permanently claimed', 1;
         INSERT dbo.GameProofSubmissionOperations
            (SubmissionOperationId,AuthenticatedPlayerUserId,GameId,AttemptId,StepNumber,CaptureSessionId,
             MediaProviderNamespace,CanonicalMediaId,State,CreatedAtUtc,UpdatedAtUtc,RecoveryAttemptCount)
         VALUES(@OperationId,@PlayerUserId,@GameId,@AttemptId,@StepNumber,@CaptureSessionId,
                @MediaProviderNamespace,@CanonicalMediaId,0,@now,@now,0);
         SET @claimId=NEWID();
         INSERT dbo.GameProofMediaClaims
          (MediaClaimId,MediaProviderNamespace,CanonicalMediaId,SubmissionOperationId,EvidenceId,
           GameId,AttemptId,StepNumber,AuthenticatedPlayerUserId,ClaimOrigin,ClaimState,
           ProviderConsumedAtUtc,EvidenceCommittedAtUtc,CreatedAtUtc,UpdatedAtUtc)
         VALUES(@claimId,@MediaProviderNamespace,@CanonicalMediaId,@OperationId,NULL,
                @GameId,@AttemptId,@StepNumber,@PlayerUserId,1,0,NULL,NULL,@now,@now);
      END;
      COMMIT TRANSACTION;
      SELECT @OperationId AS SubmissionOperationId,@claimId AS MediaClaimId;
    END TRY
    BEGIN CATCH
      IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
      THROW;
    END CATCH;
END;
GO
