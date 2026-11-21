/*
REVIEW ONLY — INERT. These are *additional* defense-in-depth candidate guards,
not approved production DDL, and not executable as distributed.
Deploy each CREATE TRIGGER statement in its own separate batch AFTER explicit
DB Coordinator authorization. Existing production application roles must NOT be
able to disable the guards. TX-A/B/C procedures need separate implementation.

IF DB_NAME() <> N'Wayhunter_M5_Registry_IsolatedTest' THROW 52100, 'Wrong DB', 1;

-- SEPARATE BATCH 1:
CREATE TRIGGER dbo.TR_M5Test_Operation_Immutable
ON dbo.GameProofSubmissionOperations AFTER UPDATE, DELETE AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i
               ON d.SubmissionOperationId=i.SubmissionOperationId
               WHERE i.SubmissionOperationId IS NULL)
        THROW 52101, 'Operation deletion/PK change forbidden', 1;
    IF EXISTS (SELECT 1 FROM inserted i LEFT JOIN deleted d
               ON d.SubmissionOperationId=i.SubmissionOperationId
               WHERE d.SubmissionOperationId IS NULL)
        THROW 52102, 'Operation PK change forbidden', 1;
    IF EXISTS (
        SELECT 1 FROM deleted d JOIN inserted i
          ON d.SubmissionOperationId=i.SubmissionOperationId
        WHERE d.GameId<>i.GameId OR d.AttemptId<>i.AttemptId
           OR d.StepNumber<>i.StepNumber OR d.CreatedAtUtc<>i.CreatedAtUtc
           OR DATALENGTH(d.AuthenticatedPlayerUserId) <> DATALENGTH(i.AuthenticatedPlayerUserId)
           OR CONVERT(varbinary(max),d.AuthenticatedPlayerUserId) <>
              CONVERT(varbinary(max),i.AuthenticatedPlayerUserId)
           OR DATALENGTH(d.CaptureSessionId) <> DATALENGTH(i.CaptureSessionId)
           OR CONVERT(varbinary(max),d.CaptureSessionId) <>
              CONVERT(varbinary(max),i.CaptureSessionId)
           OR DATALENGTH(d.MediaProviderNamespace) <> DATALENGTH(i.MediaProviderNamespace)
           OR CONVERT(varbinary(max),d.MediaProviderNamespace) <>
              CONVERT(varbinary(max),i.MediaProviderNamespace)
           OR DATALENGTH(d.CanonicalMediaId) <> DATALENGTH(i.CanonicalMediaId)
           OR CONVERT(varbinary(max),d.CanonicalMediaId) <>
              CONVERT(varbinary(max),i.CanonicalMediaId)
           OR (d.ProviderReceiptId IS NOT NULL AND
                (i.ProviderReceiptId IS NULL OR DATALENGTH(d.ProviderReceiptId) <> DATALENGTH(i.ProviderReceiptId) OR
                 CONVERT(varbinary(max),d.ProviderReceiptId) <>
                 CONVERT(varbinary(max),i.ProviderReceiptId)))
           OR (d.EvidenceId IS NOT NULL AND
                (i.EvidenceId IS NULL OR d.EvidenceId <> i.EvidenceId))
           OR (d.ProviderConsumptionConfirmedAtUtc IS NOT NULL AND
                (i.ProviderConsumptionConfirmedAtUtc IS NULL OR
                 d.ProviderConsumptionConfirmedAtUtc <> i.ProviderConsumptionConfirmedAtUtc))
           OR i.UpdatedAtUtc < d.UpdatedAtUtc
           OR d.State=4 -- Completed is immutable in this conservative test guard
    ) THROW 52103, 'Immutable operation value or completed row changed', 1;
END;

-- SEPARATE BATCH 2:
CREATE TRIGGER dbo.TR_M5Test_Evidence_AppendOnly
ON dbo.GameProofEvidence AFTER UPDATE, DELETE AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS(SELECT 1 FROM deleted)
        THROW 52104, 'Proof evidence append-only (no UPDATE/DELETE)', 1;
END;

-- Permission proposal, to be applied ONLY after security review:
-- CREATE ROLE [m5_registry_test_executor]; (see 04 base guard file)
-- DENY INSERT, UPDATE, DELETE ON OBJECT::dbo.GameProofMediaClaims TO [m5_registry_test_executor];
-- DENY INSERT, UPDATE, DELETE ON OBJECT::dbo.GameProofSubmissionOperations TO [m5_registry_test_executor];
-- DENY INSERT, UPDATE, DELETE ON OBJECT::dbo.GameProofEvidence TO [m5_registry_test_executor];
-- GRANT EXECUTE only on individually approved TX-A, TX-B, TX-C and reconciliation
-- procedures after each procedure body passes a separate code/security review.
-- No procedure bodies are currently provided or approved; do NOT grant now.
-- Database owner/sysadmin can bypass guards; test privileges from real principal.

END INERT REVIEW COMMENT.
*/
