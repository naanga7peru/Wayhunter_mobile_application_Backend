-- EXECUTABLE TEST-ONLY CANDIDATE. Phase A remains HOLD; NOT AUTHORIZED TO RUN.
:ON ERROR EXIT
-- Single SQL batch: NO GO, NO sqlcmd :r, NO USE. CREATE DATABASE only inside guarded dynamic SQL.
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF N'$(M5AuthorizedPhase)' <> N'A_CREATE' THROW 58001,'A_CREATE not explicitly selected; STOP',1;
IF ISNULL(TRY_CONVERT(int,SERVERPROPERTY('IsLocalDB')),0) <> 1 THROW 58002,'Not LocalDB; STOP',1;
IF ORIGINAL_LOGIN() COLLATE Latin1_General_100_BIN2 <> N'UNICOMTIC\ut011148' COLLATE Latin1_General_100_BIN2 THROW 58003,'Wrong Windows login; STOP',1;
IF DB_NAME() <> N'master' OR @@TRANCOUNT <> 0 THROW 58004,'Expected master and no transaction; STOP',1;
IF DB_ID(N'Wayhunter_M5_Registry_IsolatedTest') IS NOT NULL THROW 58005,'Database already exists; STOP',1;
IF ISNULL(HAS_PERMS_BY_NAME(NULL, 'SERVER','CREATE ANY DATABASE'),0) <> 1 AND ISNULL(IS_SRVROLEMEMBER('sysadmin'),0) <> 1
    THROW 58006,'Missing CREATE DATABASE permission; STOP',1;
DECLARE @db sysname=N'Wayhunter_M5_Registry_IsolatedTest', @run uniqueidentifier=CONVERT(uniqueidentifier,N'F42D6B20-B250-4F93-AB07-0478C2FA8E85');
-- CREATE DATABASE requires a separate SQL batch; dynamic EXEC is the *only* batch boundary.
-- The input is a hard-coded exact target; never substitute a name supplied by a caller.
EXEC(N'CREATE DATABASE [Wayhunter_M5_Registry_IsolatedTest] COLLATE SQL_Latin1_General_CP1_CI_AS;');
IF DB_ID(@db) IS NULL THROW 58007,'Creation outcome uncertain: manual HOLD (do not retry)',1;
-- Manifest is written in the fixed new database; if this fails, manual HOLD, never auto-DROP.
EXEC(N'USE [Wayhunter_M5_Registry_IsolatedTest];
    CREATE TABLE dbo.M5RegistryFixtureManifest(
        RunId uniqueidentifier NOT NULL PRIMARY KEY,
        CreatedAtUtc datetimeoffset(7) NOT NULL,
        CreatedBy nvarchar(256) NOT NULL,
        TestDatabaseName sysname NOT NULL
    );
    INSERT dbo.M5RegistryFixtureManifest(RunId,CreatedAtUtc,CreatedBy,TestDatabaseName)
    VALUES (CONVERT(uniqueidentifier, N''F42D6B20-B250-4F93-AB07-0478C2FA8E85''),
            TODATETIMEOFFSET(SYSUTCDATETIME(),N''+00:00''),ORIGINAL_LOGIN(),DB_NAME());');
IF NOT EXISTS(SELECT 1 FROM [Wayhunter_M5_Registry_IsolatedTest].dbo.M5RegistryFixtureManifest
 WHERE RunId=@run AND CreatedBy COLLATE Latin1_General_100_BIN2=N'UNICOMTIC\ut011148' COLLATE Latin1_General_100_BIN2
 AND TestDatabaseName=@db) THROW 58008,'Fixture manifest verification failure; manual HOLD',1;
SELECT N'Wayhunter_M5_Registry_IsolatedTest' AS ExactDatabase, DB_ID(@db) AS RecordedDatabaseId,
       CONVERT(nvarchar(33),(SELECT create_date FROM sys.databases WHERE name=@db),126) AS RecordedCreateDate,
       CONVERT(nvarchar(36),@run) AS ManifestRunId;
