-- M5 PHASE B RECOVERY CANDIDATE ONLY. EXECUTION HOLD UNTIL SEPARATE WRITTEN COORDINATOR APPROVAL.
-- Invoke from repository root, ODBC sqlcmd -I, using the exact approved baseline hash.
-- This wrapper is NEW; NEVER modify src/.../InitialSharedSchema.idempotent.sql.
:ON ERROR EXIT
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;

IF N'$(M5AuthorizedPhase)' <> N'B_BASELINE'
    THROW 59201,'STOP: wrong phase selection; variable is NOT authorization',1;
IF ISNULL(TRY_CONVERT(int, SERVERPROPERTY(N'IsLocalDB')),0) <> 1
    THROW 59202,'STOP: not LocalDB',1;
IF ORIGINAL_LOGIN() COLLATE Latin1_General_100_BIN2 <> N'UNICOMTIC\ut011148' COLLATE Latin1_General_100_BIN2
    THROW 59203,'STOP: unexpected operator',1;
IF DB_NAME() COLLATE Latin1_General_100_BIN2 <> N'Wayhunter_M5_Registry_IsolatedTest' COLLATE Latin1_General_100_BIN2
    THROW 59204,'STOP: wrong database',1;
IF DB_ID() <> 7
    THROW 59205,'STOP: changed DB identity',1;
IF @@TRANCOUNT <> 0
    THROW 59206,'STOP: preexisting transaction',1;
IF SESSIONPROPERTY(N'QUOTED_IDENTIFIER') <> 1
 OR SESSIONPROPERTY(N'ANSI_NULLS') <> 1
 OR SESSIONPROPERTY(N'ANSI_PADDING') <> 1
 OR SESSIONPROPERTY(N'ANSI_WARNINGS') <> 1
 OR SESSIONPROPERTY(N'ARITHABORT') <> 1
 OR SESSIONPROPERTY(N'CONCAT_NULL_YIELDS_NULL') <> 1
 OR SESSIONPROPERTY(N'NUMERIC_ROUNDABORT') <> 0
    THROW 59207,'STOP: required indexed-computed-column SET options missing',1;
IF OBJECT_ID(N'dbo.M5RegistryFixtureManifest',N'U') IS NULL
 OR OBJECT_ID(N'dbo.__EFMigrationsHistory',N'U') IS NULL
    THROW 59208,'STOP: missing expected recovery object',1;
IF (SELECT COUNT_BIG(*) FROM dbo.__EFMigrationsHistory) <> 0
    THROW 59209,'STOP: history is not empty',1;
IF (SELECT COUNT_BIG(*) FROM dbo.M5RegistryFixtureManifest) <> 1
 OR NOT EXISTS(SELECT 1 FROM dbo.M5RegistryFixtureManifest
    WHERE RunId=CONVERT(uniqueidentifier,N'F42D6B20-B250-4F93-AB07-0478C2FA8E85')
      AND CreatedBy COLLATE Latin1_General_100_BIN2=N'UNICOMTIC\ut011148' COLLATE Latin1_General_100_BIN2
      AND TestDatabaseName COLLATE Latin1_General_100_BIN2=N'Wayhunter_M5_Registry_IsolatedTest' COLLATE Latin1_General_100_BIN2)
    THROW 59210,'STOP: fixture manifest mismatch',1;

-- Recovery-mode only: empty history table is EXPECTED and is accepted.
-- Reject any unexpected schema objects, including tables, FKs, indexes, triggers and constraints.
DECLARE @m int=OBJECT_ID(N'dbo.M5RegistryFixtureManifest',N'U');
DECLARE @h int=OBJECT_ID(N'dbo.__EFMigrationsHistory',N'U');
IF (SELECT COUNT(*) FROM sys.columns WHERE object_id=@h)<>2
 OR (SELECT COUNT(*) FROM sys.columns WHERE object_id=@m)<>4
 OR (SELECT COUNT(*) FROM sys.key_constraints WHERE parent_object_id IN(@m,@h) AND type=N'PK')<>2
    THROW 59211,'STOP: expected object definitions differ',1;
IF EXISTS(SELECT 1 FROM sys.objects o WHERE o.is_ms_shipped=0 AND NOT (
    (o.type=N'U' AND o.object_id IN(@m,@h)) OR
    (o.type=N'PK' AND o.parent_object_id IN(@m,@h))))
    THROW 59212,'STOP: unexpected schema object/constraint',1;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=@h AND name=N'MigrationId' AND system_type_id=231 AND max_length=300 AND is_nullable=0)
 OR NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=@h AND name=N'ProductVersion' AND system_type_id=231 AND max_length=64 AND is_nullable=0)
 OR NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=@m AND name=N'RunId' AND system_type_id=36 AND is_nullable=0)
 OR NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=@m AND name=N'CreatedAtUtc' AND system_type_id=43 AND is_nullable=0)
 OR NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=@m AND name=N'CreatedBy' AND system_type_id=231 AND max_length=512 AND is_nullable=0)
 OR NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=@m AND name=N'TestDatabaseName' AND system_type_id=231 AND max_length=256 AND is_nullable=0)
    THROW 59217,'STOP: recovery object column type mismatch',1;
IF EXISTS(SELECT 1 FROM sys.schemas WHERE name NOT IN
 (N'dbo',N'guest',N'sys',N'INFORMATION_SCHEMA',N'db_owner',N'db_accessadmin',
  N'db_securityadmin',N'db_ddladmin',N'db_backupoperator',N'db_datareader',
  N'db_datawriter',N'db_denydatareader',N'db_denydatawriter'))
 OR EXISTS(SELECT 1 FROM sys.types WHERE is_user_defined=1)
 OR EXISTS(SELECT 1 FROM sys.assemblies WHERE is_user_defined=1)
    THROW 59218,'STOP: unexpected custom schema/type/assembly',1;
IF EXISTS(SELECT 1 FROM sys.indexes WHERE object_id IN (@m,@h) AND index_id>0 AND (is_primary_key<>1 OR is_unique<>1))
 OR EXISTS(SELECT 1 FROM sys.foreign_keys WHERE is_ms_shipped=0)
 OR EXISTS(SELECT 1 FROM sys.check_constraints WHERE is_ms_shipped=0)
 OR EXISTS(SELECT 1 FROM sys.default_constraints WHERE is_ms_shipped=0)
 OR EXISTS(SELECT 1 FROM sys.triggers WHERE is_ms_shipped=0)
    THROW 59213,'STOP: unexpected baseline index/constraint/trigger',1;
GO
-- SQLCMD GO does not reset connection SET options. -I independently enforces quoted identifiers.
-- Source inspected: no USE, :CONNECT, :r, or QUOTED_IDENTIFIER OFF directives.
-- Required operator cwd: repository root. Exact Git blob: 90a193505e8efe0e680ec2c94cae4d2186b3d87e.
:r .\src\Wayhunter.Infrastructure\Persistence\Migrations\InitialSharedSchema.idempotent.sql
GO
-- Postflight after existing baseline SQL migration completes its own transaction/GO.
SET NOCOUNT ON;
IF DB_NAME() <> N'Wayhunter_M5_Registry_IsolatedTest'
    THROW 59214,'STOP: database context changed during baseline',1;
IF OBJECT_ID(N'dbo.Games',N'U') IS NULL
 OR OBJECT_ID(N'dbo.GameAttempts',N'U') IS NULL
 OR OBJECT_ID(N'dbo.GameProofEvidence',N'U') IS NULL
 OR OBJECT_ID(N'dbo.GameAttemptProgress',N'U') IS NULL
    THROW 59215,'STOP: required baseline tables missing; HOLD',1;
IF (SELECT COUNT(*) FROM dbo.__EFMigrationsHistory) <> 1
 OR NOT EXISTS(SELECT 1 FROM dbo.__EFMigrationsHistory
    WHERE MigrationId=N'20261117093023_InitialSharedSchema' AND ProductVersion=N'10.0.15')
    THROW 59216,'STOP: history marker mismatch; HOLD',1;
SELECT DB_NAME() AS BaselineDatabase,DB_ID() AS DatabaseId,
       (SELECT COUNT_BIG(*) FROM dbo.__EFMigrationsHistory) AS MigrationEntries,
       SESSIONPROPERTY(N'QUOTED_IDENTIFIER') AS QuotedIdentifierFinal;
-- No Phase C instructions. Preserve DB for independent post-execution coordinator review.
