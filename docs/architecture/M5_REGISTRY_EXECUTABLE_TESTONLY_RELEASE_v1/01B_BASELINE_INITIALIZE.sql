:ON ERROR EXIT
-- APPROVAL-GATED: Do not execute before exact written authorization for B_BASELINE.
-- Required sqlcmd -v M5AuthorizedPhase=B_BASELINE; variable is an accidental-run fence,
-- NOT itself authorization. Run via -S "(localdb)\MSSQLLocalDB" -E -b -X.
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF N'$(M5AuthorizedPhase)' <> N'B_BASELINE'
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

IF OBJECT_ID(N'dbo.Games',N'U') IS NOT NULL OR OBJECT_ID(N'dbo.GameAttempts',N'U') IS NOT NULL
    THROW 57011, 'STOP: baseline objects already present', 1;
IF OBJECT_ID(N'dbo.__EFMigrationsHistory',N'U') IS NOT NULL
    THROW 57012, 'STOP: migration history already present', 1;
GO
-- sqlcmd :r resolves relative to operator working directory. Launch FROM REPOSITORY ROOT.
-- Do not substitute a different migration. The source file remains unmodified.
:r .\src\Wayhunter.Infrastructure\Persistence\Migrations\InitialSharedSchema.idempotent.sql
GO
:ON ERROR EXIT
-- APPROVAL-GATED: Do not execute before exact written authorization for B_BASELINE.
-- Required sqlcmd -v M5AuthorizedPhase=B_BASELINE; variable is an accidental-run fence,
-- NOT itself authorization. Run via -S "(localdb)\MSSQLLocalDB" -E -b -X.
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF N'$(M5AuthorizedPhase)' <> N'B_BASELINE'
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

IF OBJECT_ID(N'dbo.Games',N'U') IS NULL OR OBJECT_ID(N'dbo.GameAttempts',N'U') IS NULL
   OR OBJECT_ID(N'dbo.GameProofEvidence',N'U') IS NULL
   OR NOT EXISTS(SELECT 1 FROM dbo.__EFMigrationsHistory
                 WHERE MigrationId=N'20261117093023_InitialSharedSchema')
    THROW 57013, 'STOP: baseline initialization incomplete', 1;
SELECT DB_NAME() AS Target, COUNT(*) AS AppliedMigrationCount FROM dbo.__EFMigrationsHistory;
