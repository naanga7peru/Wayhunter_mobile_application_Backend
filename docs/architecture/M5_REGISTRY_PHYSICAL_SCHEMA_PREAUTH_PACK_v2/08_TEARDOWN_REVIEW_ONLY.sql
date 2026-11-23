/*
REVIEW ONLY — NOT EXECUTABLE. DANGER: this is a candidate DROP DATABASE.
Requires DISTINCT WRITTEN authorization for disposal of the exact database.
Never run against existing unknown data, other team fixture, or shared database.
Only after synthetic test results archived and test writer sessions closed:

USE [master];
GO
IF DB_NAME() <> N'master' THROW 52301, 'Wrong context', 1;
IF CONVERT(nvarchar(128), SERVERPROPERTY('InstanceName')) <> N'MSSQLLocalDB'
    THROW 52302, 'Wrong instance', 1;
IF DB_ID(N'Wayhunter_M5_Registry_IsolatedTest') IS NULL
    THROW 52303, 'Expected owned disposable test DB not found', 1;
-- Operator must also confirm DB creation timestamp / recorded original database_id,
-- coordinator-owned disposable DB ID, empty set of foreign user sessions,
-- approved backup/cleanup and independent confirmation of authorized DROP scope.
DROP DATABASE [Wayhunter_M5_Registry_IsolatedTest];
GO
SELECT DB_ID(N'Wayhunter_M5_Registry_IsolatedTest') AS ExpectedNull;

No DELETE/TRUNCATE of durable registry/tombstones is an acceptable replacement for
whole-database disposal. If DROP is not separately approved, retain the isolated DB,
fence all writers, report cleanup PENDING, and ask for an authorized disposition.
END INERT REVIEW COMMENT.
*/
