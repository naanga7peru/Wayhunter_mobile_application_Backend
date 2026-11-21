-- M5 PHASE B RECOVERY: READ-ONLY AUDIT ONLY. Coordinator-authorized inspection scope.
-- NO CREATE / ALTER / INSERT / UPDATE / DELETE / DROP / EXEC of user code.
:ON ERROR EXIT
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;

IF ISNULL(TRY_CONVERT(int, SERVERPROPERTY(N'IsLocalDB')), 0) <> 1
    THROW 59101, 'STOP: not LocalDB', 1;
IF DB_NAME() COLLATE Latin1_General_100_BIN2 <> N'Wayhunter_M5_Registry_IsolatedTest' COLLATE Latin1_General_100_BIN2
    THROW 59102, 'STOP: wrong DB', 1;
IF ORIGINAL_LOGIN() COLLATE Latin1_General_100_BIN2 <> N'UNICOMTIC\ut011148' COLLATE Latin1_General_100_BIN2
    THROW 59103, 'STOP: wrong operator', 1;
IF DB_ID() <> 7
    THROW 59104, 'STOP: DB identity differs from recovery report (ID 7)', 1;
IF OBJECT_ID(N'dbo.M5RegistryFixtureManifest', N'U') IS NULL OR OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NULL
    THROW 59105, 'STOP: one of two expected tables missing', 1;

DECLARE @manifest int=OBJECT_ID(N'dbo.M5RegistryFixtureManifest',N'U');
DECLARE @history int=OBJECT_ID(N'dbo.__EFMigrationsHistory',N'U');
IF (SELECT COUNT_BIG(*) FROM dbo.__EFMigrationsHistory) <> 0
    THROW 59106, 'STOP: EF migration history is not empty', 1;
IF (SELECT COUNT_BIG(*) FROM dbo.M5RegistryFixtureManifest) <> 1
    THROW 59107, 'STOP: expected exactly one manifest row', 1;
IF NOT EXISTS (
    SELECT 1 FROM dbo.M5RegistryFixtureManifest
    WHERE RunId=CONVERT(uniqueidentifier,N'F42D6B20-B250-4F93-AB07-0478C2FA8E85')
      AND CreatedBy COLLATE Latin1_General_100_BIN2=N'UNICOMTIC\ut011148' COLLATE Latin1_General_100_BIN2
      AND TestDatabaseName COLLATE Latin1_General_100_BIN2=N'Wayhunter_M5_Registry_IsolatedTest' COLLATE Latin1_General_100_BIN2
) THROW 59108, 'STOP: unexpected manifest identity', 1;
IF (SELECT COUNT(*) FROM sys.columns WHERE object_id=@history) <> 2
    THROW 59109, 'STOP: unexpected EF history columns', 1;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=@history AND name=N'MigrationId' AND system_type_id=231 AND max_length=300 AND is_nullable=0)
 OR NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=@history AND name=N'ProductVersion' AND system_type_id=231 AND max_length=64 AND is_nullable=0)
    THROW 59110, 'STOP: unexpected EF history column types', 1;
IF (SELECT COUNT(*) FROM sys.columns WHERE object_id=@manifest) <> 4
    THROW 59111, 'STOP: unexpected fixture manifest columns', 1;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=@manifest AND name=N'RunId' AND system_type_id=36 AND is_nullable=0)
 OR NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=@manifest AND name=N'CreatedAtUtc' AND system_type_id=43 AND is_nullable=0)
 OR NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=@manifest AND name=N'CreatedBy' AND system_type_id=231 AND max_length=512 AND is_nullable=0)
 OR NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=@manifest AND name=N'TestDatabaseName' AND system_type_id=231 AND max_length=256 AND is_nullable=0)
    THROW 59117, 'STOP: unexpected fixture manifest column types', 1;
IF (SELECT COUNT(*) FROM sys.key_constraints WHERE parent_object_id IN (@history,@manifest) AND type=N'PK') <> 2
    THROW 59112, 'STOP: expected two PK constraints only', 1;
IF EXISTS (
    SELECT 1 FROM sys.objects o
    WHERE o.is_ms_shipped=0 AND NOT (
        (o.type=N'U' AND o.object_id IN (@manifest,@history))
        OR (o.type=N'PK' AND o.parent_object_id IN (@manifest,@history))
    )
) THROW 59113, 'STOP: unexpected user object or constraint', 1;
IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id IN (@history,@manifest) AND index_id>0 AND (is_primary_key<>1 OR is_unique<>1))
    THROW 59114, 'STOP: unexpected non-PK index', 1;
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE is_ms_shipped=0)
 OR EXISTS (SELECT 1 FROM sys.check_constraints WHERE is_ms_shipped=0)
 OR EXISTS (SELECT 1 FROM sys.default_constraints WHERE is_ms_shipped=0)
 OR EXISTS (SELECT 1 FROM sys.triggers WHERE is_ms_shipped=0)
    THROW 59115, 'STOP: unexpected FK/CHECK/DEFAULT/trigger', 1;
IF EXISTS(SELECT 1 FROM sys.tables WHERE is_ms_shipped=0 AND object_id NOT IN (@manifest,@history))
    THROW 59116, 'STOP: unexpected table', 1;
IF EXISTS(SELECT 1 FROM sys.schemas WHERE name NOT IN
 (N'dbo',N'guest',N'sys',N'INFORMATION_SCHEMA',N'db_owner',N'db_accessadmin',
  N'db_securityadmin',N'db_ddladmin',N'db_backupoperator',N'db_datareader',
  N'db_datawriter',N'db_denydatareader',N'db_denydatawriter'))
    THROW 59118, 'STOP: unexpected custom schema', 1;
IF EXISTS(SELECT 1 FROM sys.types WHERE is_user_defined=1)
    THROW 59119, 'STOP: unexpected user-defined type', 1;
IF EXISTS(SELECT 1 FROM sys.assemblies WHERE is_user_defined=1)
    THROW 59120, 'STOP: unexpected user-defined assembly', 1;

SELECT DB_NAME() AS ExactDatabase,DB_ID() AS DatabaseId,
       ORIGINAL_LOGIN() AS OperatorLogin, CAST(SERVERPROPERTY(N'IsLocalDB') AS int) AS IsLocalDB,
       CONVERT(nvarchar(33),(SELECT create_date FROM sys.databases WHERE database_id=DB_ID()),126) AS DatabaseCreateDate,
       CAST(DATABASEPROPERTYEX(DB_NAME(),N'Collation') AS nvarchar(128)) AS DatabaseCollation,
       SESSIONPROPERTY(N'QUOTED_IDENTIFIER') AS SessionQuotedIdentifier;
SELECT RunId,CreatedAtUtc,CreatedBy,TestDatabaseName FROM dbo.M5RegistryFixtureManifest;
SELECT COUNT_BIG(*) AS ExistingHistoryEntries FROM dbo.__EFMigrationsHistory;
SELECT s.name AS ObjectSchema,o.name AS ObjectName,o.type_desc AS ObjectType
  FROM sys.objects o JOIN sys.schemas s ON s.schema_id=o.schema_id
 WHERE o.is_ms_shipped=0 ORDER BY o.type_desc,s.name,o.name;
SELECT N'PASS: permitted two-table empty-history recovery state; NO CHANGES MADE' AS AuditResult;
