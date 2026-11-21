:ON ERROR EXIT
-- APPROVAL-GATED: Do not execute before exact written authorization for A_CREATE.
-- Required sqlcmd -v M5AuthorizedPhase=A_CREATE; variable is an accidental-run fence,
-- NOT itself authorization. Run via -S "(localdb)\MSSQLLocalDB" -E -b -X.
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF N'$(M5AuthorizedPhase)' <> N'A_CREATE'
    THROW 57001, 'STOP: missing/wrong coordinator-approved phase', 1;
IF CONVERT(int,SERVERPROPERTY('IsLocalDB')) <> 1
    THROW 57002, 'STOP: not LocalDB', 1;
IF ORIGINAL_LOGIN() COLLATE Latin1_General_100_BIN2 <> N'UNICOMTIC\ut011148' COLLATE Latin1_General_100_BIN2
    THROW 57003, 'STOP: unexpected Windows login; coordinator must review', 1;
IF DB_NAME() <> N'master'
    THROW 57004, 'STOP: expected master', 1;

IF DB_ID(N'Wayhunter_M5_Registry_IsolatedTest') IS NOT NULL
    THROW 57009, 'STOP: target exists; never overwrite', 1;
GO
-- Autocommit required; distinct Phase A approval only.
CREATE DATABASE [Wayhunter_M5_Registry_IsolatedTest] COLLATE SQL_Latin1_General_CP1_CI_AS;
GO
USE [Wayhunter_M5_Registry_IsolatedTest];
GO
-- Independent post-create guard for marker installation.
:ON ERROR EXIT
-- APPROVAL-GATED: Do not execute before exact written authorization for A_CREATE.
-- Required sqlcmd -v M5AuthorizedPhase=A_CREATE; variable is an accidental-run fence,
-- NOT itself authorization. Run via -S "(localdb)\MSSQLLocalDB" -E -b -X.
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF N'$(M5AuthorizedPhase)' <> N'A_CREATE'
    THROW 57001, 'STOP: missing/wrong coordinator-approved phase', 1;
IF CONVERT(int,SERVERPROPERTY('IsLocalDB')) <> 1
    THROW 57002, 'STOP: not LocalDB', 1;
IF ORIGINAL_LOGIN() COLLATE Latin1_General_100_BIN2 <> N'UNICOMTIC\ut011148' COLLATE Latin1_General_100_BIN2
    THROW 57003, 'STOP: unexpected Windows login; coordinator must review', 1;
IF DB_NAME() <> N'Wayhunter_M5_Registry_IsolatedTest'
    THROW 57005, 'STOP: expected dedicated isolated database', 1;
IF DB_NAME() IN (N'master',N'model',N'msdb',N'tempdb')
    THROW 57006, 'STOP: system database', 1;

IF OBJECT_ID(N'dbo.M5RegistryFixtureManifest',N'U') IS NOT NULL
    THROW 57010, 'STOP: manifest already exists', 1;
CREATE TABLE dbo.M5RegistryFixtureManifest (
    RunId uniqueidentifier NOT NULL PRIMARY KEY,
    CreatedAtUtc datetimeoffset(7) NOT NULL,
    CreatedBy nvarchar(256) NOT NULL,
    TestDatabaseName sysname NOT NULL
);
INSERT dbo.M5RegistryFixtureManifest (RunId,CreatedAtUtc,CreatedBy,TestDatabaseName)
VALUES (CONVERT(uniqueidentifier,'F42D6B20-B250-4F93-AB07-0478C2FA8E85'), TODATETIMEOFFSET(SYSUTCDATETIME(),'+00:00'),
        ORIGINAL_LOGIN(),DB_NAME());
SELECT DB_NAME() AS TestDatabase,DB_ID() AS RecordedDatabaseId,
       (SELECT create_date FROM sys.databases WHERE database_id=DB_ID()) AS DatabaseCreateDate,
       RunId,CreatedAtUtc,CreatedBy FROM dbo.M5RegistryFixtureManifest;
