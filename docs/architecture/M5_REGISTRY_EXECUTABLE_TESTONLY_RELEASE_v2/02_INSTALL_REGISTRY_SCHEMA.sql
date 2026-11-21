:ON ERROR EXIT
-- APPROVAL-GATED: Do not execute before exact written authorization for C_SCHEMA.
-- Required sqlcmd -v M5AuthorizedPhase=C_SCHEMA; variable is an accidental-run fence,
-- NOT itself authorization. Run via -S "(localdb)\MSSQLLocalDB" -E -b -X.
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF N'$(M5AuthorizedPhase)' <> N'C_SCHEMA'
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

-- Fail on existing/broken baseline, not attempt automatic reconciliation.
IF OBJECT_ID(N'dbo.Games',N'U') IS NULL OR OBJECT_ID(N'dbo.GameAttempts',N'U') IS NULL
   OR OBJECT_ID(N'dbo.GameAttemptProgress',N'U') IS NULL
   OR OBJECT_ID(N'dbo.GameProofEvidence',N'U') IS NULL
    THROW 57014, 'STOP: required baseline objects missing', 1;
IF OBJECT_ID(N'dbo.GameProofMediaClaims',N'U') IS NOT NULL
   OR OBJECT_ID(N'dbo.GameProofSubmissionOperations',N'U') IS NOT NULL
    THROW 57015, 'STOP: operation/registry already installed', 1;
IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.GameAttempts')
                 AND name=N'AK_GameAttempts_Id_Game_Player' AND type='UQ')
    THROW 57016, 'STOP: required GameAttempts alternate key missing', 1;
IF EXISTS (SELECT 1 FROM dbo.GameAttempts) OR EXISTS(SELECT 1 FROM dbo.GameProofEvidence)
    THROW 57017, 'STOP: unexpected existing baseline data; never mutate', 1;
IF (SELECT c.collation_name FROM sys.columns c WHERE c.object_id=OBJECT_ID(N'dbo.GameAttempts')
    AND c.name=N'PlayerUserId') <> CONVERT(nvarchar(128),DATABASEPROPERTYEX(DB_NAME(),'Collation'))
    THROW 57018, 'STOP: principal player collation mismatch', 1;
IF EXISTS (SELECT 1 FROM sys.key_constraints WHERE name=N'AK_GameProofEvidence_Id_Game_Attempt_Step'
 AND parent_object_id=OBJECT_ID(N'dbo.GameProofEvidence'))
    THROW 57019, 'STOP: proposed evidence alternate key already exists', 1;
BEGIN TRY
BEGIN TRANSACTION;
-- Test-only dependency; NOT the production table or a shared migration.
-- Existing proposed operation model: 20 properties (including rowversion).
CREATE TABLE dbo.GameProofSubmissionOperations (
    SubmissionOperationId uniqueidentifier NOT NULL,
    AuthenticatedPlayerUserId nvarchar(256) NOT NULL,
    GameId uniqueidentifier NOT NULL,
    AttemptId uniqueidentifier NOT NULL,
    StepNumber int NOT NULL,
    CaptureSessionId nvarchar(256) NOT NULL,
    MediaProviderNamespace nvarchar(64) COLLATE Latin1_General_100_BIN2 NOT NULL,
    CanonicalMediaId nvarchar(256) COLLATE Latin1_General_100_BIN2 NOT NULL,
    State int NOT NULL CONSTRAINT DF_M5Test_OperationState DEFAULT (0),
    CreatedAtUtc datetimeoffset(7) NOT NULL,
    UpdatedAtUtc datetimeoffset(7) NOT NULL,
    NextReconciliationAtUtc datetimeoffset(7) NULL,
    ClaimToken nvarchar(128) NULL,
    LeaseExpiresAtUtc datetimeoffset(7) NULL,
    ProviderConsumptionConfirmedAtUtc datetimeoffset(7) NULL,
    ProviderReceiptId nvarchar(256) NULL,
    EvidenceId uniqueidentifier NULL,
    LastErrorCode nvarchar(128) NULL,
    RecoveryAttemptCount int NOT NULL CONSTRAINT DF_M5Test_RecoveryCount DEFAULT (0),
    RowVersion rowversion NOT NULL,
    CONSTRAINT PK_GameProofSubmissionOperations PRIMARY KEY NONCLUSTERED (SubmissionOperationId),
    CONSTRAINT CK_GameProofOperations_State CHECK (State IN (0,1,2,3,4,5,6)),
    CONSTRAINT CK_GameProofOperations_Step CHECK (StepNumber >= 1),
    CONSTRAINT CK_GameProofOperations_RecoveryAttempts CHECK (RecoveryAttemptCount >= 0),
    CONSTRAINT FK_M5Test_Operation_AttemptOwner FOREIGN KEY (AttemptId, GameId, AuthenticatedPlayerUserId)
        REFERENCES dbo.GameAttempts (Id, GameId, PlayerUserId) ON DELETE NO ACTION ON UPDATE NO ACTION,
    CONSTRAINT AK_GameProofOperations_ImmutableClaimOwner UNIQUE NONCLUSTERED
        (SubmissionOperationId, GameId, AttemptId, AuthenticatedPlayerUserId, StepNumber,
         MediaProviderNamespace, CanonicalMediaId)
);
CREATE UNIQUE NONCLUSTERED INDEX UX_GameProofOperations_Provider_Media
    ON dbo.GameProofSubmissionOperations (MediaProviderNamespace, CanonicalMediaId);
CREATE UNIQUE NONCLUSTERED INDEX UX_GameProofOperations_Provider_Capture
    ON dbo.GameProofSubmissionOperations (MediaProviderNamespace, CaptureSessionId);
CREATE NONCLUSTERED INDEX IX_GameProofOperations_Reconciliation
    ON dbo.GameProofSubmissionOperations (State, NextReconciliationAtUtc);

-- Existing GameProofEvidence has PK and AK(Id,AttemptId), but not the v3
-- four-column principal key; only the isolated test DB may receive this change.
ALTER TABLE dbo.GameProofEvidence
    ADD CONSTRAINT AK_GameProofEvidence_Id_Game_Attempt_Step UNIQUE NONCLUSTERED
        (Id, GameId, AttemptId, StepNumber);

-- Registry: EXACT 16 v3 columns, candidate type lengths and nullability.
CREATE TABLE dbo.GameProofMediaClaims (
    MediaClaimId uniqueidentifier NOT NULL,
    MediaProviderNamespace nvarchar(64) COLLATE Latin1_General_100_BIN2 NOT NULL,
    CanonicalMediaId nvarchar(256) COLLATE Latin1_General_100_BIN2 NOT NULL,
    SubmissionOperationId uniqueidentifier NULL,
    EvidenceId uniqueidentifier NULL,
    GameId uniqueidentifier NOT NULL,
    AttemptId uniqueidentifier NOT NULL,
    StepNumber int NOT NULL,
    AuthenticatedPlayerUserId nvarchar(256) NOT NULL,
    ClaimOrigin tinyint NOT NULL,
    ClaimState tinyint NOT NULL,
    ProviderConsumedAtUtc datetimeoffset(7) NULL,
    EvidenceCommittedAtUtc datetimeoffset(7) NULL,
    CreatedAtUtc datetimeoffset(7) NOT NULL,
    UpdatedAtUtc datetimeoffset(7) NOT NULL,
    RowVersion rowversion NOT NULL,
    CONSTRAINT PK_GameProofMediaClaims PRIMARY KEY NONCLUSTERED (MediaClaimId),
    CONSTRAINT CK_GameProofMediaClaims_IdsAndStep CHECK (
        MediaClaimId <> '00000000-0000-0000-0000-000000000000'
        AND GameId <> '00000000-0000-0000-0000-000000000000'
        AND AttemptId <> '00000000-0000-0000-0000-000000000000'
        AND StepNumber >= 1
        AND (SubmissionOperationId IS NULL OR SubmissionOperationId <> '00000000-0000-0000-0000-000000000000')
        AND (EvidenceId IS NULL OR EvidenceId <> '00000000-0000-0000-0000-000000000000')
    ),
    CONSTRAINT CK_GameProofMediaClaims_IdentityNonblank CHECK (
        DATALENGTH(MediaProviderNamespace) > 0
        AND DATALENGTH(CanonicalMediaId) > 0
        AND DATALENGTH(AuthenticatedPlayerUserId) > 0
        AND LEN(LTRIM(RTRIM(MediaProviderNamespace))) > 0
        AND LEN(LTRIM(RTRIM(CanonicalMediaId))) > 0
        AND LEN(LTRIM(RTRIM(AuthenticatedPlayerUserId))) > 0
    ),
    CONSTRAINT CK_GameProofMediaClaims_OriginState CHECK (
        ClaimOrigin IN (1,2) AND ClaimState IN (0,1,2,3,4)
        AND ((ClaimOrigin = 1 AND SubmissionOperationId IS NOT NULL AND ClaimState IN (0,1,2,3))
         OR (ClaimOrigin = 2 AND SubmissionOperationId IS NULL AND ClaimState = 4 AND EvidenceId IS NOT NULL))
    ),
    CONSTRAINT CK_GameProofMediaClaims_StateFields CHECK (
           (ClaimState = 0 AND EvidenceId IS NULL AND ProviderConsumedAtUtc IS NULL AND EvidenceCommittedAtUtc IS NULL)
        OR (ClaimState = 1 AND EvidenceId IS NULL AND ProviderConsumedAtUtc IS NOT NULL AND EvidenceCommittedAtUtc IS NULL)
        OR (ClaimState = 2 AND EvidenceId IS NOT NULL AND ProviderConsumedAtUtc IS NOT NULL AND EvidenceCommittedAtUtc IS NOT NULL)
        OR (ClaimState = 3 AND EvidenceId IS NULL AND ProviderConsumedAtUtc IS NULL AND EvidenceCommittedAtUtc IS NULL)
        OR (ClaimState = 4 AND EvidenceId IS NOT NULL AND ProviderConsumedAtUtc IS NULL AND EvidenceCommittedAtUtc IS NULL)
    ),
    CONSTRAINT CK_GameProofMediaClaims_TimeOrder CHECK (
        UpdatedAtUtc >= CreatedAtUtc
        AND (ProviderConsumedAtUtc IS NULL OR ProviderConsumedAtUtc >= CreatedAtUtc)
        AND (EvidenceCommittedAtUtc IS NULL OR
             (ProviderConsumedAtUtc IS NOT NULL AND EvidenceCommittedAtUtc >= ProviderConsumedAtUtc))
        AND (EvidenceCommittedAtUtc IS NULL OR UpdatedAtUtc >= EvidenceCommittedAtUtc)
    ),
    CONSTRAINT FK_M5Test_Claim_AttemptOwner FOREIGN KEY (AttemptId, GameId, AuthenticatedPlayerUserId)
        REFERENCES dbo.GameAttempts(Id, GameId, PlayerUserId) ON DELETE NO ACTION ON UPDATE NO ACTION,
    CONSTRAINT FK_M5Test_Claim_ImmutableOperation FOREIGN KEY
        (SubmissionOperationId, GameId, AttemptId, AuthenticatedPlayerUserId, StepNumber,
         MediaProviderNamespace, CanonicalMediaId)
        REFERENCES dbo.GameProofSubmissionOperations
        (SubmissionOperationId, GameId, AttemptId, AuthenticatedPlayerUserId, StepNumber,
         MediaProviderNamespace, CanonicalMediaId) ON DELETE NO ACTION ON UPDATE NO ACTION,
    CONSTRAINT FK_M5Test_Claim_ProofEvidence FOREIGN KEY (EvidenceId, GameId, AttemptId, StepNumber)
        REFERENCES dbo.GameProofEvidence(Id, GameId, AttemptId, StepNumber)
        ON DELETE NO ACTION ON UPDATE NO ACTION
);
CREATE UNIQUE NONCLUSTERED INDEX UX_GameProofMediaClaims_Provider_Canonical
    ON dbo.GameProofMediaClaims (MediaProviderNamespace, CanonicalMediaId);
CREATE UNIQUE NONCLUSTERED INDEX UX_GameProofMediaClaims_Operation
    ON dbo.GameProofMediaClaims (SubmissionOperationId) WHERE SubmissionOperationId IS NOT NULL;
CREATE UNIQUE NONCLUSTERED INDEX UX_GameProofMediaClaims_Evidence
    ON dbo.GameProofMediaClaims (EvidenceId) WHERE EvidenceId IS NOT NULL;
CREATE NONCLUSTERED INDEX IX_GameProofMediaClaims_Attempt
    ON dbo.GameProofMediaClaims (AttemptId, StepNumber, ClaimState);
CREATE NONCLUSTERED INDEX IX_GameProofMediaClaims_Reconciliation
    ON dbo.GameProofMediaClaims (ClaimState, UpdatedAtUtc);
COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
