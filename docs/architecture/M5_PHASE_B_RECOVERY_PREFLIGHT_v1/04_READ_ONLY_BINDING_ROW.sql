-- Read-only, headerless identity row for the linked LocalDB alias and named pipe.
-- Invoke with -E -I -b -V 11 -W -h -1 -w 2048; zero DDL or writes.
:ON ERROR EXIT
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SELECT CONCAT(
  N'M5BIND|',
  ISNULL(CONVERT(nvarchar(256),SERVERPROPERTY(N'ServerName')),N'<NULL>'),N'|',
  ISNULL(CONVERT(nvarchar(256),@@SERVERNAME),N'<NULL>'),N'|',
  ISNULL(CONVERT(nvarchar(256),SERVERPROPERTY(N'InstanceName')),N'<NULL>'),N'|',
  ISNULL(CONVERT(nvarchar(256),SERVERPROPERTY(N'MachineName')),N'<NULL>'),N'|',
  ISNULL(CONVERT(nvarchar(10),SERVERPROPERTY(N'IsLocalDB')),N'<NULL>'),N'|',
  ORIGINAL_LOGIN(),N'|',DB_NAME(),N'|',CONVERT(nvarchar(12),DB_ID()),N'|',
  ISNULL(CONVERT(nvarchar(23),(SELECT create_date FROM sys.databases WHERE database_id=DB_ID()),121),N'<NULL>'),N'|',
  CONVERT(nvarchar(10),SESSIONPROPERTY(N'QUOTED_IDENTIFIER')),N'|',
  CONVERT(nvarchar(12),(SELECT COUNT_BIG(*) FROM dbo.__EFMigrationsHistory)),N'|',
  CONVERT(nvarchar(12),(SELECT COUNT_BIG(*) FROM dbo.M5RegistryFixtureManifest)),N'|',
  ISNULL((SELECT CONVERT(nvarchar(36),RunId) FROM dbo.M5RegistryFixtureManifest),N'<NULL>'),N'|',
  ISNULL((SELECT CreatedBy FROM dbo.M5RegistryFixtureManifest),N'<NULL>'),N'|',
  ISNULL((SELECT TestDatabaseName FROM dbo.M5RegistryFixtureManifest),N'<NULL>'),N'|',
  CONVERT(nvarchar(12),ISNULL(IS_SRVROLEMEMBER(N'sysadmin'),0)),N'|',
  CONVERT(nvarchar(12),(SELECT COUNT_BIG(*) FROM sys.dm_exec_sessions
    WHERE is_user_process=1 AND session_id<>@@SPID AND database_id=DB_ID()))
) AS BindingRow;
