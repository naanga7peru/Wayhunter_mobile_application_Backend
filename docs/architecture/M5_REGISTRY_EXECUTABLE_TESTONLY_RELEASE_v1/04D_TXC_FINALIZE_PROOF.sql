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

IF OBJECT_ID(N'dbo.M5Test_TXC_FinalizeProof',N'P') IS NOT NULL THROW 57021, 'Existing procedure; no overwrite', 1;
GO
CREATE PROCEDURE dbo.M5Test_TXC_FinalizeProof
  @OperationId uniqueidentifier,
  @GameId uniqueidentifier,
  @EvidenceId uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF DB_NAME() <> N'Wayhunter_M5_Registry_IsolatedTest' OR CONVERT(int,SERVERPROPERTY('IsLocalDB'))<>1
        THROW 57101, 'STOP wrong database/server during procedure call', 1;

-- Synthetic stage finalization ONLY; no M4 provider call and NO rewards/admin approvals.
    IF @EvidenceId='00000000-0000-0000-0000-000000000000' OR @EvidenceId IS NULL
      THROW 57131,'Nonempty stable evidence ID required',1;
    DECLARE @now datetimeoffset(7)=TODATETIMEOFFSET(SYSUTCDATETIME(),'+00:00');
    DECLARE @AttemptId uniqueidentifier,@Step int,@Player nvarchar(256),@Media nvarchar(256);
    DECLARE @claimedAt datetimeoffset(7), @opState int,@claimState tinyint,
            @storedEvidence uniqueidentifier;
    BEGIN TRY
      SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
      BEGIN TRANSACTION;
      IF NOT EXISTS(SELECT 1 FROM dbo.Games WITH (UPDLOCK,HOLDLOCK) WHERE Id=@GameId)
        THROW 57132,'Game absent',1;
      -- Get immutable binding only after Game lock; subsequent Attempt/Progress locks.
      SELECT @AttemptId=AttemptId,@Step=StepNumber,@Player=AuthenticatedPlayerUserId,
             @Media=CanonicalMediaId
        FROM dbo.GameProofSubmissionOperations
        WHERE SubmissionOperationId=@OperationId AND GameId=@GameId;
      IF @AttemptId IS NULL THROW 57133,'Operation missing',1;
      IF NOT EXISTS(SELECT 1 FROM dbo.GameAttempts WITH (UPDLOCK,HOLDLOCK)
        WHERE Id=@AttemptId AND GameId=@GameId AND PlayerUserId COLLATE Latin1_General_100_BIN2 = @Player COLLATE Latin1_General_100_BIN2)
        THROW 57134,'Attempt owner changed',1;
      IF NOT EXISTS(SELECT 1 FROM dbo.GameAttemptProgress WITH (UPDLOCK,HOLDLOCK)
        WHERE AttemptId=@AttemptId AND GameId=@GameId AND PlayerUserId COLLATE Latin1_General_100_BIN2 = @Player COLLATE Latin1_General_100_BIN2
          )
        THROW 57135,'Progress owner missing',1;
      SELECT @opState=State,@storedEvidence=EvidenceId
        FROM dbo.GameProofSubmissionOperations WITH (UPDLOCK,HOLDLOCK)
        WHERE SubmissionOperationId=@OperationId AND GameId=@GameId
          AND ProviderReceiptId IS NOT NULL;
      IF @opState=4
      BEGIN
        -- Read-only replay: return the same evidence, never another insert/advance.
        IF @storedEvidence=@EvidenceId
          AND EXISTS (SELECT 1 FROM dbo.GameProofMediaClaims WITH (UPDLOCK,HOLDLOCK)
            WHERE SubmissionOperationId=@OperationId AND GameId=@GameId
              AND ClaimState=2 AND EvidenceId=@EvidenceId)
          AND EXISTS (SELECT 1 FROM dbo.GameProofEvidence WHERE Id=@EvidenceId
             AND GameId=@GameId AND AttemptId=@AttemptId AND StepNumber=@Step)
          AND EXISTS (SELECT 1 FROM dbo.GameAttemptProgress WHERE AttemptId=@AttemptId AND GameId=@GameId
            AND Status=2 AND CurrentStepNumber=@Step)
        BEGIN
          COMMIT TRANSACTION;
          SELECT @OperationId AS SubmissionOperationId,@EvidenceId AS EvidenceId;
          RETURN;
        END;
        THROW 57143,'Completed operation is inconsistent; HOLD',1;
      END;
      IF @opState<>3 OR @opState IS NULL THROW 57136,'No durable receipt',1;
      IF NOT EXISTS(SELECT 1 FROM dbo.GameAttemptProgress WHERE AttemptId=@AttemptId
                   AND GameId=@GameId AND CurrentStepNumber=@Step AND Status=1)
        THROW 57144,'Progress changed before new finalization',1;
      SELECT @claimState=ClaimState,@claimedAt=ProviderConsumedAtUtc
        FROM dbo.GameProofMediaClaims WITH (UPDLOCK,HOLDLOCK)
        WHERE SubmissionOperationId=@OperationId AND GameId=@GameId AND ClaimState=1;
      IF @claimState<>1 OR @claimState IS NULL OR @claimedAt IS NULL
        THROW 57137,'Operation/claim not jointly confirmed',1;
      IF EXISTS(SELECT 1 FROM dbo.GameProofEvidence WITH (UPDLOCK,HOLDLOCK) WHERE Id=@EvidenceId)
        THROW 57138,'Evidence ID already used',1;
      IF EXISTS(SELECT 1 FROM dbo.GameProofEvidence AS e WITH (UPDLOCK,HOLDLOCK)
       WHERE e.GameId=@GameId AND e.AttemptId=@AttemptId AND e.StepNumber=@Step
         AND NOT EXISTS (SELECT 1 FROM dbo.GameProofReviewDecisions r WHERE r.EvidenceId=e.Id))
         THROW 57139,'Pending proof already exists',1;
      INSERT dbo.GameProofEvidence(Id,GameId,AttemptId,StepNumber,MediaId,SubmittedAtUtc)
        VALUES(@EvidenceId,@GameId,@AttemptId,@Step,@Media,@now);
      UPDATE dbo.GameAttemptProgress SET Status=2
        WHERE AttemptId=@AttemptId AND GameId=@GameId AND Status=1 AND CurrentStepNumber=@Step;
      IF @@ROWCOUNT<>1 THROW 57140,'Progress concurrency failure',1;
      UPDATE dbo.GameProofSubmissionOperations SET State=4,EvidenceId=@EvidenceId,
             NextReconciliationAtUtc=NULL,UpdatedAtUtc=@now
        WHERE SubmissionOperationId=@OperationId AND GameId=@GameId AND State=3;
      IF @@ROWCOUNT<>1 THROW 57141,'Operation completion failed',1;
      UPDATE dbo.GameProofMediaClaims SET ClaimState=2,EvidenceId=@EvidenceId,
             EvidenceCommittedAtUtc=@now,UpdatedAtUtc=@now
        WHERE SubmissionOperationId=@OperationId AND GameId=@GameId AND ClaimState=1;
      IF @@ROWCOUNT<>1 THROW 57142,'Registry finalization failed',1;
      COMMIT TRANSACTION;
      SELECT @OperationId AS SubmissionOperationId,@EvidenceId AS EvidenceId;
    END TRY
    BEGIN CATCH
      IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
      THROW;
    END CATCH;
END;
GO
