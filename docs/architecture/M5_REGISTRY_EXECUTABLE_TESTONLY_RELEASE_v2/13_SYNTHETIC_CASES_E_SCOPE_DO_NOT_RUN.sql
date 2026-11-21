-- Test-only proposed executable synthetic dispatcher. E_SYNTHETIC HOLD: DO NOT RUN.
:ON ERROR EXIT
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET LOCK_TIMEOUT 15000;
IF N'$(M5AuthorizedPhase)'<>N'E_SYNTHETIC' OR
 ISNULL(TRY_CONVERT(int,SERVERPROPERTY('IsLocalDB')),0)<>1 OR
 DB_NAME()<>N'Wayhunter_M5_Registry_IsolatedTest' OR
 ORIGINAL_LOGIN() COLLATE Latin1_General_100_BIN2<>N'UNICOMTIC\ut011148' COLLATE Latin1_General_100_BIN2
    THROW 59500,'Wrong approved E_SYNTHETIC SQL scope',1;
IF NOT EXISTS(SELECT 1 FROM dbo.M5RegistryFixtureManifest
  WHERE RunId='F42D6B20-B250-4F93-AB07-0478C2FA8E85')
    THROW 59501,'Fixture identity mismatch',1;
DECLARE @Case nvarchar(8)=N'$(M5TestFilter)';
IF @Case NOT IN(N'S01',N'S02',N'S03',N'S04',N'S05',N'S06',N'S07',N'S08',N'S09',N'S10',N'S11',N'S12',N'S13',N'S14',N'S15')
    THROW 59502,'Explicit recognized test filter required',1;
IF @Case=N'S01'
BEGIN
  IF OBJECT_ID(N'dbo.GameProofMediaClaims',N'U') IS NULL
    OR (SELECT COUNT(*) FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.GameProofMediaClaims'))<>5
    OR OBJECT_ID(N'dbo.M5TestSyntheticConsumedReceipts',N'U') IS NULL
    OR OBJECT_ID(N'dbo.M5Test_ClaimFinalizer',N'P') IS NULL
     THROW 59503,'S01 metadata incomplete',1;
  SELECT @Case AS TestId,N'PASS: object and five CHECK existence' AS Result;
  RETURN;
END;
-- Only synthetic, invented identities; never import/copy any real historical evidence or user.
DECLARE @now datetimeoffset(7)=TODATETIMEOFFSET(SYSUTCDATETIME(),N'+00:00'),
 @g uniqueidentifier=NEWID(),@a uniqueidentifier=NEWID(),@stepId uniqueidentifier=NEWID(),
 @o uniqueidentifier=NEWID(),@e uniqueidentifier=NEWID(),
 @player nvarchar(256),@media nvarchar(256),@cap nvarchar(256),
 @ns nvarchar(64)=N'm5-localdb-synthetic-only',
 @lease nvarchar(128)=CONVERT(nvarchar(36),NEWID()),
 @final nvarchar(128)=CONVERT(nvarchar(36),NEWID()),
 @receipt nvarchar(256)=CONCAT(N'fake-consumed-',CONVERT(nvarchar(36),NEWID()));
SET @player=CONCAT(N'm5-synthetic-',CONVERT(nvarchar(36),@g));
SET @media=CONCAT(N'synthetic-asset-',CONVERT(nvarchar(36),@g));
SET @cap=CONCAT(N'synthetic-session-',CONVERT(nvarchar(36),@g));
INSERT dbo.Users(UserId,DisplayName,NormalizedEmail,PasswordHash,Status,CreatedAtUtc)
 VALUES(@player,N'Synthetic M5',CONCAT(N'm5+',CONVERT(nvarchar(36),@g),N'@example.invalid'),N'FAKE_ONLY_DO_NOT_LOGIN',1,@now);
INSERT dbo.Games(Id,CreatorUserId,Title,Description,DurationMinutes,Status,ApprovedAtUtc,ExpiresAtUtc,IsFlaggedForReview)
 VALUES(@g,@player,N'M5 synthetic test',N'No real user/provenance',60,2,@now,DATEADD(hour,720,@now),0);
INSERT dbo.GameSteps(Id,GameId,StepNumber) VALUES(@stepId,@g,1);
INSERT dbo.GameAttempts(Id,GameId,PlayerUserId,StartedAtUtc,DeadlineUtc)
 VALUES(@a,@g,@player,@now,DATEADD(hour,1,@now));
INSERT dbo.GameAttemptProgress(AttemptId,GameId,PlayerUserId,TotalSteps,CurrentStepNumber,RejectedEvidenceCount,Status)
 VALUES(@a,@g,@player,1,1,0,1);
IF @Case=N'S15'
BEGIN
 DECLARE @e2 uniqueidentifier=NEWID();
 INSERT dbo.GameProofEvidence(Id,AttemptId,GameId,StepNumber,MediaId,SubmittedAtUtc)
 VALUES(@e,@a,@g,1,@media,@now),(@e2,@a,@g,1,CONCAT(@media,N'-2'),@now);
 BEGIN TRY
  UPDATE dbo.GameProofEvidence SET MediaId=N'wrong' WHERE Id IN (@e,@e2);
  THROW 59526,'S15 evidence multi-row UPDATE succeeded',1;
 END TRY BEGIN CATCH IF ERROR_NUMBER()<>52104 THROW; END CATCH;
 BEGIN TRY
  DELETE FROM dbo.GameProofEvidence WHERE Id IN (@e,@e2);
  THROW 59527,'S15 evidence multi-row DELETE succeeded',1;
 END TRY BEGIN CATCH IF ERROR_NUMBER()<>52104 THROW; END CATCH;
 IF (SELECT COUNT(*) FROM dbo.GameProofEvidence WHERE Id IN (@e,@e2))<>2
   THROW 59528,'S15 evidence tampered after failure',1;
 SELECT @Case AS TestId,N'PASS: two-row evidence UPDATE/DELETE rejected' AS Result;
 RETURN;
END;
IF @Case=N'S03'
BEGIN
 BEGIN TRY
  EXEC dbo.M5Test_TXA_RegisterReserve @OperationId=@o,@GameId=@g,@AttemptId=@a,@StepNumber=1,
   @PlayerUserId=@player,@CaptureSessionId=@cap,@MediaProviderNamespace=@ns,@CanonicalMediaId=@media,
   @FaultPoint=N'AFTER_OPERATION_INSERT';
  THROW 59504,'S03 did not inject fault',1;
 END TRY BEGIN CATCH
  IF ERROR_NUMBER()<>58701 THROW;
 END CATCH;
 IF EXISTS(SELECT 1 FROM dbo.GameProofSubmissionOperations WHERE SubmissionOperationId=@o)
  OR EXISTS(SELECT 1 FROM dbo.GameProofMediaClaims WHERE SubmissionOperationId=@o)
  THROW 59505,'S03 rollback not atomic',1;
 SELECT @Case AS TestId,N'PASS: TX-A rolled back both rows' AS Result;
 RETURN;
END;
EXEC dbo.M5Test_TXA_RegisterReserve @OperationId=@o,@GameId=@g,@AttemptId=@a,@StepNumber=1,
 @PlayerUserId=@player,@CaptureSessionId=@cap,@MediaProviderNamespace=@ns,@CanonicalMediaId=@media;
IF @Case=N'S02'
BEGIN
 IF (SELECT COUNT(*) FROM dbo.GameProofSubmissionOperations WHERE SubmissionOperationId=@o)<>1
 OR (SELECT COUNT(*) FROM dbo.GameProofMediaClaims WHERE SubmissionOperationId=@o AND ClaimState=0)<>1
 THROW 59506,'S02 TX-A mismatch',1;
 SELECT @Case AS TestId,N'PASS: one operation and claim' AS Result;
 RETURN;
END;
IF @Case IN (N'S08',N'S13',N'S14')
BEGIN
 DECLARE @o2 uniqueidentifier=NEWID(),@cap2 nvarchar(256)=CONCAT(@cap,N'-2'),
 @media2 nvarchar(256)=CONCAT(@media,N'-2');
 EXEC dbo.M5Test_TXA_RegisterReserve @OperationId=@o2,@GameId=@g,@AttemptId=@a,@StepNumber=1,
 @PlayerUserId=@player,@CaptureSessionId=@cap2,@MediaProviderNamespace=@ns,@CanonicalMediaId=@media2;
 IF @Case=N'S13'
 BEGIN
  BEGIN TRY
   UPDATE dbo.GameProofSubmissionOperations SET UpdatedAtUtc=DATEADD(SECOND,1,UpdatedAtUtc)
    WHERE SubmissionOperationId IN(@o,@o2);
   THROW 59520,'S13 multi-row illegal operation state-0 rewrite succeeded',1;
  END TRY BEGIN CATCH IF ERROR_NUMBER()<>52103 THROW; END CATCH;
  SELECT @Case AS TestId,N'PASS: two operation rows rejected' AS Result;
  RETURN;
 END;
 IF @Case=N'S14'
 BEGIN
  UPDATE dbo.GameProofMediaClaims SET ClaimState=3,UpdatedAtUtc=DATEADD(SECOND,1,UpdatedAtUtc)
   WHERE SubmissionOperationId IN(@o,@o2);
  BEGIN TRY
   UPDATE dbo.GameProofMediaClaims SET ClaimState=0,UpdatedAtUtc=DATEADD(SECOND,1,UpdatedAtUtc)
    WHERE SubmissionOperationId IN(@o,@o2);
   THROW 59521,'S14 two tombstones were released',1;
  END TRY BEGIN CATCH IF ERROR_NUMBER()<>51023 THROW; END CATCH;
  IF (SELECT COUNT(*) FROM dbo.GameProofMediaClaims WHERE SubmissionOperationId IN(@o,@o2) AND ClaimState=3)<>2
   THROW 59522,'S14 tombstone rollback failed',1;
  SELECT @Case AS TestId,N'PASS: two terminal tombstones retained' AS Result;
  RETURN;
 END;
 BEGIN TRY
  UPDATE dbo.GameProofMediaClaims SET ClaimState=0 WHERE SubmissionOperationId IN(@o,@o2);
  THROW 59507,'S08 illegal multi-row state update succeeded',1;
 END TRY BEGIN CATCH
  IF ERROR_NUMBER()<>51023 THROW;
 END CATCH;
 IF (SELECT COUNT(*) FROM dbo.GameProofMediaClaims WHERE SubmissionOperationId IN(@o,@o2) AND ClaimState=0)<>2
  THROW 59508,'S08 unexpected post-trigger state',1;
 SELECT @Case AS TestId,N'PASS: multi-row illegal transition denied' AS Result;
 RETURN;
END;
EXEC dbo.M5Test_ClaimForFakeProvider @OperationId=@o,@GameId=@g,@ClaimToken=@lease,@LeaseMinutes=5;
-- Approved E_SYNTHETIC runner owner inserts fake attestation, not real M4.
INSERT dbo.M5TestSyntheticConsumedReceipts
(ProviderReceiptId,SubmissionOperationId,GameId,AttemptId,StepNumber,AuthenticatedPlayerUserId,
 CaptureSessionId,MediaProviderNamespace,CanonicalMediaId,IsFakeConfirmedConsumed,ObservedAtUtc)
VALUES(@receipt,@o,@g,@a,1,@player,@cap,@ns,@media,1,@now);
IF @Case=N'S04'
BEGIN
 BEGIN TRY
  EXEC dbo.M5Test_TXB_ConfirmConsumption @OperationId=@o,@GameId=@g,@ClaimToken=@lease,
   @ProviderReceiptId=@receipt,@AttemptId=@a,@StepNumber=1,@AuthenticatedPlayerUserId=@player,
   @CaptureSessionId=@cap,@MediaProviderNamespace=@ns,@CanonicalMediaId=N'wrong-asset';
  THROW 59509,'S04 mismatched canonical binding accepted',1;
 END TRY BEGIN CATCH
  IF ERROR_NUMBER()<>57130 THROW;
 END CATCH;
 IF EXISTS(SELECT 1 FROM dbo.GameProofSubmissionOperations WHERE SubmissionOperationId=@o AND ProviderReceiptId IS NOT NULL)
  OR NOT EXISTS(SELECT 1 FROM dbo.GameProofMediaClaims WHERE SubmissionOperationId=@o AND ClaimState=0)
  THROW 59510,'S04 receipt wrongly persisted',1;
 SELECT @Case AS TestId,N'PASS: wrong media receipt rejected' AS Result;
 RETURN;
END;
IF @Case=N'S09'
BEGIN
 BEGIN TRY
  EXEC dbo.M5Test_TXB_ConfirmConsumption @OperationId=@o,@GameId=@g,@ClaimToken=@lease,
   @ProviderReceiptId=N'unknown-receipt',@AttemptId=@a,@StepNumber=1,@AuthenticatedPlayerUserId=@player,
   @CaptureSessionId=@cap,@MediaProviderNamespace=@ns,@CanonicalMediaId=@media;
  THROW 59511,'S09 unknown fake receipt accepted',1;
 END TRY BEGIN CATCH
  IF ERROR_NUMBER()<>57131 THROW;
 END CATCH;
 SELECT @Case AS TestId,N'PASS: missing synthetic attestation rejected' AS Result;
 RETURN;
END;
IF @Case=N'S05'
BEGIN
 BEGIN TRY
  EXEC dbo.M5Test_TXB_ConfirmConsumption @OperationId=@o,@GameId=@g,@ClaimToken=@lease,
   @ProviderReceiptId=@receipt,@AttemptId=@a,@StepNumber=1,@AuthenticatedPlayerUserId=@player,
   @CaptureSessionId=@cap,@MediaProviderNamespace=@ns,@CanonicalMediaId=@media,
   @FaultPoint=N'AFTER_OPERATION_CONFIRM';
  THROW 59512,'S05 did not inject fault',1;
 END TRY BEGIN CATCH
  IF ERROR_NUMBER()<>58702 THROW;
 END CATCH;
 IF NOT EXISTS(SELECT 1 FROM dbo.GameProofSubmissionOperations WHERE SubmissionOperationId=@o AND State=1 AND ProviderReceiptId IS NULL)
 OR NOT EXISTS(SELECT 1 FROM dbo.GameProofMediaClaims WHERE SubmissionOperationId=@o AND ClaimState=0)
 THROW 59513,'S05 atomic rollback failed',1;
 SELECT @Case AS TestId,N'PASS: TX-B op/claim rolled back' AS Result;
 RETURN;
END;
EXEC dbo.M5Test_TXB_ConfirmConsumption @OperationId=@o,@GameId=@g,@ClaimToken=@lease,
 @ProviderReceiptId=@receipt,@AttemptId=@a,@StepNumber=1,@AuthenticatedPlayerUserId=@player,
 @CaptureSessionId=@cap,@MediaProviderNamespace=@ns,@CanonicalMediaId=@media;
IF @Case=N'S06'
BEGIN
 BEGIN TRY
  EXEC dbo.M5Test_TXC_FinalizeProof @OperationId=@o,@GameId=@g,@EvidenceId=@e,@FinalizerToken=N'stale-holder';
  THROW 59514,'S06 stale holder accepted',1;
 END TRY BEGIN CATCH
  IF ERROR_NUMBER()<>57145 THROW;
 END CATCH;
 IF EXISTS(SELECT 1 FROM dbo.GameProofEvidence WHERE Id=@e)
  THROW 59515,'S06 stale holder persisted evidence',1;
 SELECT @Case AS TestId,N'PASS: missing/stale finalizer token rejected' AS Result;
 RETURN;
END;
DECLARE @finalizerLeaseSeconds int=CASE WHEN @Case=N'S11' THEN 5 ELSE 300 END;
EXEC dbo.M5Test_ClaimFinalizer @OperationId=@o,@GameId=@g,@FinalizerToken=@final,@LeaseSeconds=@finalizerLeaseSeconds;
IF @Case=N'S11'
BEGIN
 -- Test-only real 6-second hold for 5-second lease. No fake clocks or unapproved backdating.
 WAITFOR DELAY '00:00:06';
 BEGIN TRY
  EXEC dbo.M5Test_TXC_FinalizeProof @OperationId=@o,@GameId=@g,@EvidenceId=@e,@FinalizerToken=@final;
  THROW 59523,'S11 expired finalizer committed evidence',1;
 END TRY BEGIN CATCH IF ERROR_NUMBER()<>57145 THROW; END CATCH;
 IF EXISTS(SELECT 1 FROM dbo.GameProofEvidence WHERE Id=@e) THROW 59524,'S11 evidence persisted',1;
 SELECT @Case AS TestId,N'PASS: finalizer lease expiry rejected' AS Result;
 RETURN;
END;
IF @Case=N'S12'
BEGIN
 BEGIN TRY
  EXEC dbo.M5Test_ClaimFinalizer @OperationId=@o,@GameId=@g,
   @FinalizerToken=N'other-holder',@LeaseSeconds=300;
  THROW 59525,'S12 active lease was stolen',1;
 END TRY BEGIN CATCH IF ERROR_NUMBER()<>58607 THROW; END CATCH;
 SELECT @Case AS TestId,N'PASS: second holder rejected while lease active' AS Result;
 RETURN;
END;
IF @Case=N'S07'
BEGIN
 EXEC dbo.M5Test_TXC_FinalizeProof @OperationId=@o,@GameId=@g,@EvidenceId=@e,@FinalizerToken=@final;
 IF NOT EXISTS(SELECT 1 FROM dbo.GameProofSubmissionOperations WHERE SubmissionOperationId=@o AND State=4 AND EvidenceId=@e)
  OR NOT EXISTS(SELECT 1 FROM dbo.GameProofMediaClaims WHERE SubmissionOperationId=@o AND ClaimState=2 AND EvidenceId=@e)
  OR NOT EXISTS(SELECT 1 FROM dbo.GameAttemptProgress WHERE AttemptId=@a AND Status=2)
  OR NOT EXISTS(SELECT 1 FROM dbo.GameProofEvidence WHERE Id=@e)
  THROW 59516,'S07 transaction not coherent',1;
 SELECT @Case AS TestId,N'PASS: TX-C evidence/progress/operation/registry atomic' AS Result;
 RETURN;
END;
IF @Case=N'S10'
BEGIN
 BEGIN TRY
  EXEC dbo.M5Test_TXC_FinalizeProof @OperationId=@o,@GameId=@g,@EvidenceId=@e,
   @FinalizerToken=@final,@FaultPoint=N'AFTER_EVIDENCE_INSERT';
  THROW 59517,'S10 fault not triggered',1;
 END TRY BEGIN CATCH
  IF ERROR_NUMBER()<>58703 THROW;
 END CATCH;
 IF EXISTS(SELECT 1 FROM dbo.GameProofEvidence WHERE Id=@e)
  OR NOT EXISTS(SELECT 1 FROM dbo.GameAttemptProgress WHERE AttemptId=@a AND Status=1)
  OR NOT EXISTS(SELECT 1 FROM dbo.GameProofSubmissionOperations WHERE SubmissionOperationId=@o AND State=3 AND EvidenceId IS NULL)
  OR NOT EXISTS(SELECT 1 FROM dbo.GameProofMediaClaims WHERE SubmissionOperationId=@o AND ClaimState=1)
    THROW 59519,'S10 TX-C atomic rollback failed',1;
 SELECT @Case AS TestId,N'PASS: TX-C fault rollback' AS Result;
 RETURN;
END;
-- S06/S07 and fault-injection variants are separate scopes; not all concurrency cases implemented.
SELECT @Case AS TestId,N'EXPLICIT TEST INCOMPLETE; HOLD' AS Result;
THROW 59518,'No test implementation for given filter',1;
