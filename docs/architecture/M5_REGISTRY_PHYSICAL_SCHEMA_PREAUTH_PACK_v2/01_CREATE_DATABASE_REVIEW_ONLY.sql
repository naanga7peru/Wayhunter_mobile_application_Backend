/*
REVIEW-ONLY / INERT. DO NOT UNCOMMENT OR EXECUTE. No database creation is authorized.
Exact candidate CREATE step for separately authorized, disposable LocalDB only.
Approved instance expected: (localdb)\MSSQLLocalDB.
Expected connected database: master; use Windows integrated authentication.

USE [master];
GO
IF DB_NAME() <> N'master'
    THROW 52001, 'M5 STOP: expected master', 1;
IF CONVERT(nvarchar(128), SERVERPROPERTY('InstanceName')) <> N'MSSQLLocalDB'
    THROW 52002, 'M5 STOP: instance name not MSSQLLocalDB', 1;
IF DB_ID(N'Wayhunter_M5_Registry_IsolatedTest') IS NOT NULL
    THROW 52003, 'M5 STOP: target database already exists; never overwrite', 1;
-- Independent operator check REQUIRED: exactly approved local instance and Windows
-- login, empty/disposable target, coordinator grant for CREATE and later DROP.
CREATE DATABASE [Wayhunter_M5_Registry_IsolatedTest]
    COLLATE SQL_Latin1_General_CP1_CI_AS;
GO
-- Proposed explicit collation matches the observed LocalDB server collation.
-- M4 provider/canonical columns are separately proposed as Latin1_General_100_BIN2.
IF DB_ID(N'Wayhunter_M5_Registry_IsolatedTest') IS NULL
    THROW 52004, 'M5 STOP: target not created as expected', 1;
SELECT name, state_desc, collation_name, create_date
FROM sys.databases WHERE name = N'Wayhunter_M5_Registry_IsolatedTest';

END INERT REVIEW COMMENT.
*/
