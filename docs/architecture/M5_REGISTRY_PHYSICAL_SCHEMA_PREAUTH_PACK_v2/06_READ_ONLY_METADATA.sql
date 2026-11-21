-- READ ONLY. Allowed only within approved read-only environment-inspection scope.
-- Connect to master; do not use against shared/production application databases.
SET NOCOUNT ON;
SELECT DB_NAME() AS ConnectedDatabase, @@SERVERNAME AS ServerName,
       ORIGINAL_LOGIN() AS OriginalLogin,
       CAST(SERVERPROPERTY('InstanceName') AS nvarchar(128)) AS InstanceName,
       CAST(SERVERPROPERTY('ProductVersion') AS nvarchar(40)) AS ProductVersion,
       CAST(SERVERPROPERTY('Collation') AS nvarchar(128)) AS ServerCollation;
SELECT name, state_desc, collation_name, create_date
FROM sys.databases
WHERE name=N'Wayhunter_M5_Registry_IsolatedTest';
-- Zero rows in second query = target was absent/hidden at inspection time.
-- Do NOT assume shared DBs are safe targets just because they are visible.
