:ON ERROR EXIT
-- APPROVAL-GATED: Do not execute before exact written authorization for F_DISPOSE.
-- Required sqlcmd -v M5AuthorizedPhase=F_DISPOSE; variable is an accidental-run fence,
-- NOT itself authorization. Run via -S "(localdb)\MSSQLLocalDB" -E -b -X.
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF N'$(M5AuthorizedPhase)' <> N'F_DISPOSE'
    THROW 57001, 'STOP: missing/wrong coordinator-approved phase', 1;
IF CONVERT(int,SERVERPROPERTY('IsLocalDB')) <> 1
    THROW 57002, 'STOP: not LocalDB', 1;
IF ORIGINAL_LOGIN() COLLATE Latin1_General_100_BIN2 <> N'UNICOMTIC\ut011148' COLLATE Latin1_General_100_BIN2
    THROW 57003, 'STOP: unexpected Windows login; coordinator must review', 1;
IF DB_NAME() <> N'master'
    THROW 57004, 'STOP: expected master', 1;

IF DB_ID(N'Wayhunter_M5_Registry_IsolatedTest') IS NULL THROW 57301,'STOP target does not exist',1;
IF N'$(M5RecordedDatabaseId)' = N'' OR N'$(M5RecordedCreateDate)' = N''
    THROW 57302,'STOP missing independently recorded identity',1;
IF DB_ID(N'Wayhunter_M5_Registry_IsolatedTest') <> TRY_CONVERT(int,N'$(M5RecordedDatabaseId)')
 OR (SELECT CONVERT(nvarchar(33),create_date,126) FROM sys.databases WHERE name=N'Wayhunter_M5_Registry_IsolatedTest')
      <> N'$(M5RecordedCreateDate)'
    THROW 57303,'STOP database id/create_date mismatch',1;
IF NOT EXISTS(SELECT 1 FROM [Wayhunter_M5_Registry_IsolatedTest].dbo.M5RegistryFixtureManifest
              WHERE RunId=CONVERT(uniqueidentifier,'F42D6B20-B250-4F93-AB07-0478C2FA8E85')
                AND CreatedBy COLLATE Latin1_General_100_BIN2=N'UNICOMTIC\ut011148' COLLATE Latin1_General_100_BIN2)
    THROW 57304,'STOP fixture manifest mismatch',1;
IF (SELECT COUNT(*) FROM sys.dm_exec_sessions
     WHERE database_id=DB_ID(N'Wayhunter_M5_Registry_IsolatedTest') AND session_id<>@@SPID)>0
    THROW 57305,'STOP other connected sessions; never force SINGLE_USER',1;
GO
-- DROP DATABASE must be alone in its batch. Run only with a SEPARATE written Phase F approval
-- and a retained sanitized evidence archive. Ordinary rollback is disable/fence, NOT delete claims.
DROP DATABASE [Wayhunter_M5_Registry_IsolatedTest];
GO
SELECT DB_ID(N'Wayhunter_M5_Registry_IsolatedTest') AS ExpectedNull;
