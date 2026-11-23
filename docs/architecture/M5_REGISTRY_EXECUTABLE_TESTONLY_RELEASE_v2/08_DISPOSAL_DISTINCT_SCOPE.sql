-- EXECUTABLE TEST-ONLY CANDIDATE. Phase F remains HOLD; NOT AUTHORIZED TO RUN.
:ON ERROR EXIT
-- Single SQL batch: NO GO. DROP DATABASE is executed only from *inside* the preflight-guarded branch.
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF N'$(M5AuthorizedPhase)' <> N'F_DISPOSE' THROW 58301,'F_DISPOSE not selected; STOP',1;
IF N'$(M5DisposeExplicitConsent)' <> N'YES_APPROVED_F_DISPOSE' THROW 58302,'Missing separate disposal consent; STOP',1;
IF ISNULL(TRY_CONVERT(int,SERVERPROPERTY('IsLocalDB')),0)<>1 THROW 58303,'Not LocalDB',1;
IF ORIGINAL_LOGIN() COLLATE Latin1_General_100_BIN2<>N'UNICOMTIC\ut011148' COLLATE Latin1_General_100_BIN2 THROW 58304,'Wrong original login',1;
IF DB_NAME()<>N'master' OR @@TRANCOUNT<>0 THROW 58305,'Expected master outside transaction',1;
DECLARE @db sysname=N'Wayhunter_M5_Registry_IsolatedTest', @id int=DB_ID(N'Wayhunter_M5_Registry_IsolatedTest'),
        @recordedId int=TRY_CONVERT(int,N'$(M5RecordedDatabaseId)'),
        @recordedCreate nvarchar(33)=N'$(M5RecordedCreateDate)';
IF @id IS NULL OR @recordedId IS NULL OR @id<>@recordedId OR @recordedCreate=N''
    THROW 58306,'Database ID/record mismatch; STOP',1;
IF (SELECT CONVERT(nvarchar(33),create_date,126) FROM sys.databases WHERE database_id=@id)<>@recordedCreate
    THROW 58307,'Create date mismatch; STOP',1;
IF OBJECT_ID(N'[Wayhunter_M5_Registry_IsolatedTest].dbo.M5RegistryFixtureManifest',N'U') IS NULL
    THROW 58308,'Fixture manifest missing; STOP',1;
IF NOT EXISTS(SELECT 1 FROM [Wayhunter_M5_Registry_IsolatedTest].dbo.M5RegistryFixtureManifest WHERE
    RunId=CONVERT(uniqueidentifier,N'F42D6B20-B250-4F93-AB07-0478C2FA8E85') AND
    CreatedBy COLLATE Latin1_General_100_BIN2=N'UNICOMTIC\ut011148' COLLATE Latin1_General_100_BIN2
    AND TestDatabaseName=@db) THROW 58309,'Manifest mismatch; STOP',1;
IF EXISTS(SELECT 1 FROM sys.dm_exec_sessions WHERE database_id=@id AND session_id<>@@SPID)
    THROW 58310,'Active sessions; no force disconnect',1;
-- Never silently destroy evidence or test residues. Separate Coordinator disposition of retained rows required.
IF EXISTS(SELECT 1 FROM [Wayhunter_M5_Registry_IsolatedTest].dbo.GameProofMediaClaims)
 OR EXISTS(SELECT 1 FROM [Wayhunter_M5_Registry_IsolatedTest].dbo.GameProofSubmissionOperations)
 OR EXISTS(SELECT 1 FROM [Wayhunter_M5_Registry_IsolatedTest].dbo.GameProofEvidence)
 OR EXISTS(SELECT 1 FROM [Wayhunter_M5_Registry_IsolatedTest].dbo.GameAttempts)
 OR EXISTS(SELECT 1 FROM [Wayhunter_M5_Registry_IsolatedTest].dbo.GameAttemptProgress)
 OR EXISTS(SELECT 1 FROM [Wayhunter_M5_Registry_IsolatedTest].dbo.Users)
    THROW 58311,'Synthetic/test rows remain; STOP; separate approved cleanup and archived evidence required',1;
-- Unique hard-coded name, same guarded batch; no standalone DROP after GO.
EXEC(N'DROP DATABASE [Wayhunter_M5_Registry_IsolatedTest];');
SELECT DB_ID(@db) AS MustBeNull;
