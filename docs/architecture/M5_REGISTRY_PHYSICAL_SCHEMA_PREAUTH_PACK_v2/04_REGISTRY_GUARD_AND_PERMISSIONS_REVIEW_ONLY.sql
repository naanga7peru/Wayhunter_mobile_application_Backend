/*
REVIEW ONLY — INERT SQL; NOT APPROVED FOR EXECUTION.
Candidate test-only immutable registry guard and restricted writer. Requires approved
DB role/procedure ownership decisions, explicit test DB, preflight and a separate
SQL execution authorization. Not production-ready.

IF DB_NAME() <> N'Wayhunter_M5_Registry_IsolatedTest'
    THROW 51020, 'Wrong test database; abort', 1;

-- PROCEDURE OWNER / ROLE MODEL: 
-- A vetted schema owner has DML through ownership chaining; application/test executor
-- principal has no direct DML. The actual TX-A/B/C stored-procedure bodies are STILL
-- TO BE REVIEWED/IMPLEMENTED. Grant EXECUTE only to those named, approved procedures.
CREATE ROLE [m5_registry_test_executor];
DENY INSERT, UPDATE, DELETE ON OBJECT::dbo.GameProofMediaClaims TO [m5_registry_test_executor];
DENY INSERT, UPDATE, DELETE ON OBJECT::dbo.GameProofSubmissionOperations TO [m5_registry_test_executor];
DENY INSERT, UPDATE, DELETE ON OBJECT::dbo.GameProofEvidence TO [m5_registry_test_executor];
-- Test executor must NOT have db_owner/db_ddladmin/db_datawriter or direct table DML.
-- No DB-wide EXECUTE grant, no db_owner membership. No GRANT until vetted procs exist.
-- Review these exact proposed GRANT targets separately:
-- GRANT EXECUTE ON OBJECT::dbo.M5Test_RegisterAndReserve TO [m5_registry_test_executor];
-- GRANT EXECUTE ON OBJECT::dbo.M5Test_ConfirmConsumption TO [m5_registry_test_executor];
-- GRANT EXECUTE ON OBJECT::dbo.M5Test_FinalizeProof TO [m5_registry_test_executor];
-- GRANT EXECUTE ON OBJECT::dbo.M5Test_Reconcile TO [m5_registry_test_executor];
-- The GRANT lines are comments, NOT effective permissions.

-- REQUIRED BEFORE AN EXECUTABLE RELEASE: put this CREATE TRIGGER in a
-- separate SQL Server batch from the preceding CREATE ROLE/DENY statements.
-- It is deliberately an inert reviewer template, not an executable script.
-- Multirow UPDATE/DELETE guard, AFTER trigger on registry table; not a substitute
-- for reviewed cross-table TX-B/TX-C procedures. Do not casually disable it.
CREATE TRIGGER dbo.TR_M5Test_GameProofMediaClaims_Immutable
ON dbo.GameProofMediaClaims AFTER UPDATE, DELETE AS
BEGIN
    SET NOCOUNT ON;
    -- DELETE (including one row deleted by UPDATE primary-key rewriting) is forbidden.
    IF EXISTS (
        SELECT 1 FROM deleted AS d LEFT JOIN inserted AS i
            ON i.MediaClaimId = d.MediaClaimId
        WHERE i.MediaClaimId IS NULL
    )
        THROW 51021, 'Media claims are permanently owned; DELETE is forbidden', 1;

    -- Detect newly inserted PK on UPDATE as well as deleted/missing old PK.
    IF EXISTS (SELECT 1 FROM inserted i LEFT JOIN deleted d
               ON i.MediaClaimId = d.MediaClaimId WHERE d.MediaClaimId IS NULL)
        THROW 51024, 'Media claim PK reassignment is forbidden', 1;

    IF EXISTS (
        SELECT 1
        FROM deleted AS d INNER JOIN inserted AS i
            ON i.MediaClaimId = d.MediaClaimId
        WHERE i.GameId <> d.GameId OR i.AttemptId <> d.AttemptId
           OR i.StepNumber <> d.StepNumber
           OR i.ClaimOrigin <> d.ClaimOrigin
           OR ISNULL(i.SubmissionOperationId, '00000000-0000-0000-0000-000000000000') <>
              ISNULL(d.SubmissionOperationId, '00000000-0000-0000-0000-000000000000')
           OR i.CreatedAtUtc <> d.CreatedAtUtc
           OR DATALENGTH(i.MediaProviderNamespace) <> DATALENGTH(d.MediaProviderNamespace)
           OR CONVERT(varbinary(max), i.MediaProviderNamespace) <>
              CONVERT(varbinary(max), d.MediaProviderNamespace)
           OR DATALENGTH(i.CanonicalMediaId) <> DATALENGTH(d.CanonicalMediaId)
           OR CONVERT(varbinary(max), i.CanonicalMediaId) <>
              CONVERT(varbinary(max), d.CanonicalMediaId)
           OR DATALENGTH(i.AuthenticatedPlayerUserId) <> DATALENGTH(d.AuthenticatedPlayerUserId)
           OR CONVERT(varbinary(max), i.AuthenticatedPlayerUserId) <>
              CONVERT(varbinary(max), d.AuthenticatedPlayerUserId)
    )
        THROW 51022, 'Media claim immutable identity changed', 1;

    -- Exact state machine: 0->1, 0->3, 1->2 only. No 0->0 writes;
    -- no 2/3/4 updates, delete, retry writes or tombstone release.
    IF EXISTS (
        SELECT 1 FROM deleted d JOIN inserted i ON i.MediaClaimId = d.MediaClaimId
        WHERE NOT ((d.ClaimState = 0 AND i.ClaimState IN (1,3)) OR
                   (d.ClaimState = 1 AND i.ClaimState = 2))
           OR i.UpdatedAtUtc < d.UpdatedAtUtc
           OR (d.ProviderConsumedAtUtc IS NOT NULL AND
               (i.ProviderConsumedAtUtc IS NULL OR i.ProviderConsumedAtUtc <> d.ProviderConsumedAtUtc))
           OR (d.EvidenceCommittedAtUtc IS NOT NULL AND
               (i.EvidenceCommittedAtUtc IS NULL OR i.EvidenceCommittedAtUtc <> d.EvidenceCommittedAtUtc))
           OR (d.EvidenceId IS NOT NULL AND
               (i.EvidenceId IS NULL OR i.EvidenceId <> d.EvidenceId))
           OR (d.ClaimState = 0 AND i.ClaimState IN (1,3) AND
               (i.EvidenceId IS NOT NULL))
           OR (d.ClaimState = 1 AND i.ClaimState = 2 AND
               (i.EvidenceId IS NULL OR i.ProviderConsumedAtUtc <> d.ProviderConsumedAtUtc))
    )
        THROW 51023, 'Forbidden media-claim update transition', 1;
END;

-- Caveats requiring reviewer signoff:
-- 1. Role DENY and stored-procedure ownership chaining are not enough unless the
--    caller actually lacks inherited privileged permissions / table ownership.
-- 2. A sysadmin/db_owner may bypass/disable triggers; separate deployment controls.
-- 3. Guard does not prove op receipt or cross-table matching; TX-B/TX-C procedures do.
-- 4. Operation and evidence tables need their own restricted writers/guards.
-- 5. Trigger behavior in multi-row, UPDATE PK, FK cascade and deadlock cases must be tested.
-- 6. No permissions are granted to a real user in this script.
-- 7. Trigger requires a separate GO batch in an executable release; this file is inert.

END REVIEW-ONLY COMMENT; NO SQL IN THIS FILE IS EXECUTABLE AS DISTRIBUTED.
*/
