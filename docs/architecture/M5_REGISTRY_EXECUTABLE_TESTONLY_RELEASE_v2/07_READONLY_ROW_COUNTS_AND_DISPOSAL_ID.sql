:ON ERROR EXIT
-- Phase E inspection after separate authorization; READ ONLY; does not clean/delete.
SET NOCOUNT ON;
IF N'$(M5AuthorizedPhase)'<>N'E_INSPECT'
    THROW 57400,'Wrong phase',1;
IF ISNULL(TRY_CONVERT(int,SERVERPROPERTY('IsLocalDB')),0)<>1 OR DB_NAME()<>N'Wayhunter_M5_Registry_IsolatedTest'
    THROW 57401,'Wrong server/database',1;
IF ORIGINAL_LOGIN() COLLATE Latin1_General_100_BIN2<>N'UNICOMTIC\ut011148' COLLATE Latin1_General_100_BIN2
    THROW 57402,'Wrong original login',1;
IF NOT EXISTS(SELECT 1 FROM dbo.M5RegistryFixtureManifest
   WHERE RunId='F42D6B20-B250-4F93-AB07-0478C2FA8E85')
    THROW 57403,'Fixture manifest absent',1;
SELECT N'GameProofSubmissionOperations' AS TableName,COUNT_BIG(*) AS RowCount FROM dbo.GameProofSubmissionOperations
UNION ALL SELECT N'GameProofMediaClaims',COUNT_BIG(*) FROM dbo.GameProofMediaClaims
UNION ALL SELECT N'GameProofEvidence',COUNT_BIG(*) FROM dbo.GameProofEvidence
UNION ALL SELECT N'GameAttemptProgress',COUNT_BIG(*) FROM dbo.GameAttemptProgress
UNION ALL SELECT N'GameAttempts',COUNT_BIG(*) FROM dbo.GameAttempts;
SELECT DB_NAME() AS DatabaseName,DB_ID() AS DatabaseId,
 (SELECT create_date FROM sys.databases WHERE database_id=DB_ID()) AS CreateDate,
 RunId,CreatedAtUtc,CreatedBy FROM dbo.M5RegistryFixtureManifest;
