:ON ERROR EXIT
-- APPROVAL-GATED: Do not execute before exact written authorization for D_GUARDS.
-- Required sqlcmd -v M5AuthorizedPhase=D_GUARDS; variable is an accidental-run fence,
-- NOT itself authorization. Run via -S "(localdb)\MSSQLLocalDB" -E -b -X.
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF N'$(M5AuthorizedPhase)' <> N'D_GUARDS'
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

IF OBJECT_ID(N'dbo.GameProofMediaClaims',N'U') IS NULL THROW 57020, 'Registry not installed', 1;
IF OBJECT_ID(N'dbo.TR_M5Test_GameProofMediaClaims_Immutable',N'TR') IS NOT NULL THROW 57021, 'Trigger exists', 1;
GO
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
GO
