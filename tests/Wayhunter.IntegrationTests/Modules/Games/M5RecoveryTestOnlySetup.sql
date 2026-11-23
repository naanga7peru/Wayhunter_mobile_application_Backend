-- M5 ISOLATED TEST-ONLY SQL SETUP. Never apply to shared or production databases.
SET NOCOUNT ON;
IF DB_NAME() <> N'Wayhunter_M5_IntegrationTest' THROW 51010, 'Wrong database', 1;
IF COALESCE(CONVERT(int, SERVERPROPERTY('IsLocalDB')), 0) <> 1 THROW 51011, 'LocalDB required', 1;
IF NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId = N'20261117093023_InitialSharedSchema') THROW 51012, 'Expected baseline migration missing', 1;
IF EXISTS (SELECT 1 FROM sys.partitions p JOIN sys.tables t ON t.object_id=p.object_id WHERE t.name <> N'__EFMigrationsHistory' AND p.index_id IN (0,1) AND p.rows > 0) THROW 51013, 'Existing application data detected', 1;
IF OBJECT_ID(N'dbo.GameProofSubmissionOperations',N'U') IS NOT NULL THROW 51014, 'Recovery table already exists', 1;
CREATE TABLE dbo.GameProofSubmissionOperations (
 SubmissionOperationId uniqueidentifier NOT NULL,
 AuthenticatedPlayerUserId nvarchar(256) NOT NULL,
 GameId uniqueidentifier NOT NULL,
 AttemptId uniqueidentifier NOT NULL,
 StepNumber int NOT NULL,
 CaptureSessionId nvarchar(256) NOT NULL,
 MediaProviderNamespace nvarchar(64) NOT NULL,
 CanonicalMediaId nvarchar(256) NOT NULL,
 State int NOT NULL,
 CreatedAtUtc datetimeoffset(7) NOT NULL,
 UpdatedAtUtc datetimeoffset(7) NOT NULL,
 NextReconciliationAtUtc datetimeoffset(7) NULL,
 ClaimToken nvarchar(128) NULL,
 LeaseExpiresAtUtc datetimeoffset(7) NULL,
 ProviderConsumptionConfirmedAtUtc datetimeoffset(7) NULL,
 ProviderReceiptId nvarchar(256) NULL,
 EvidenceId uniqueidentifier NULL,
 LastErrorCode nvarchar(128) NULL,
 RecoveryAttemptCount int NOT NULL,
 RowVersion rowversion NOT NULL,
 CONSTRAINT PK_GameProofSubmissionOperations PRIMARY KEY (SubmissionOperationId),
 CONSTRAINT FK_GameProofOperations_AttemptOwner FOREIGN KEY (AttemptId,GameId,AuthenticatedPlayerUserId) REFERENCES dbo.GameAttempts(Id,GameId,PlayerUserId),
 CONSTRAINT CK_GameProofOperations_State CHECK (State IN (0,1,2,3,4,5,6)),
 CONSTRAINT CK_GameProofOperations_Step CHECK (StepNumber >= 1),
 CONSTRAINT CK_GameProofOperations_RecoveryAttempts CHECK (RecoveryAttemptCount >= 0)
);
CREATE UNIQUE INDEX UX_GameProofOperations_Provider_Media ON dbo.GameProofSubmissionOperations(MediaProviderNamespace,CanonicalMediaId);
CREATE UNIQUE INDEX UX_GameProofOperations_Provider_Capture ON dbo.GameProofSubmissionOperations(MediaProviderNamespace,CaptureSessionId);
CREATE INDEX IX_GameProofOperations_Reconciliation ON dbo.GameProofSubmissionOperations(State,NextReconciliationAtUtc);
SELECT DB_NAME() AS DatabaseName, OBJECT_ID(N'dbo.GameProofSubmissionOperations',N'U') AS RecoveryTableId;
