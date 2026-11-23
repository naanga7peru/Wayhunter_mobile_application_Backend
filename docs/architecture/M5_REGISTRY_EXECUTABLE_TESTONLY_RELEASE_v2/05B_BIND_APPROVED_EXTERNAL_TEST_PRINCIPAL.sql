-- D_SECURITY TEST-ONLY candidate: externally approved Windows login binding.
-- DO NOT execute. Grant requires exact external identity and separate DB Coordinator authorization.
:ON ERROR EXIT
SET NOCOUNT ON; SET XACT_ABORT ON;
IF N'$(M5AuthorizedPhase)'<>N'D_SECURITY' OR
   ISNULL(TRY_CONVERT(int,SERVERPROPERTY('IsLocalDB')),0)<>1 OR
   DB_NAME()<>N'Wayhunter_M5_Registry_IsolatedTest' OR
   ORIGINAL_LOGIN() COLLATE Latin1_General_100_BIN2<>N'UNICOMTIC\ut011148' COLLATE Latin1_General_100_BIN2
    THROW 58801,'Wrong phase/server/operator/database',1;
IF NOT EXISTS(SELECT 1 FROM dbo.M5RegistryFixtureManifest WHERE RunId=N'F42D6B20-B250-4F93-AB07-0478C2FA8E85')
    THROW 58802,'Manifest mismatch',1;
-- sqlcmd substitutes exact identity only. SQL uses QUOTENAME, no injected identifier.
DECLARE @external sysname=N'$(M5ApprovedExternalLogin)';
IF @external IS NULL OR LEN(@external)<4 OR LEFT(@external,1)=N'$'
   OR @external COLLATE Latin1_General_100_BIN2=N'UNICOMTIC\ut011148' COLLATE Latin1_General_100_BIN2
   OR SUSER_ID(@external) IS NULL
    THROW 58803,'External Windows login must exist and differ from owner; STOP',1;
IF DATABASE_PRINCIPAL_ID(@external) IS NOT NULL
    THROW 58804,'Database user already exists; no automatic reuse',1;
IF DATABASE_PRINCIPAL_ID(N'm5_registry_test_executor') IS NULL
    THROW 58805,'Executor role missing',1;
IF ISNULL(IS_SRVROLEMEMBER(N'sysadmin',@external),1)<>0
    THROW 58806,'External principal must not be sysadmin',1;
DECLARE @q nvarchar(258)=QUOTENAME(@external);
EXEC(N'CREATE USER '+@q+N' FOR LOGIN '+@q+N';');
EXEC(N'ALTER ROLE [m5_registry_test_executor] ADD MEMBER '+@q+N';');
SELECT @external AS ApprovedExternalLogin, USER_ID(@external) AS ExternalDbUserId,
    N'Actual external token login check must be separately performed using 05C' AS VerificationNext;
