-- E_INSPECT external Windows login effective permission check; READ ONLY.
-- Must CONNECT AS THE EXTERNAL WINDOWS ACCOUNT, not admin EXECUTE AS USER/LOGIN.
:ON ERROR EXIT
SET NOCOUNT ON;
IF N'$(M5AuthorizedPhase)'<>N'E_INSPECT' OR
 ISNULL(TRY_CONVERT(int,SERVERPROPERTY('IsLocalDB')),0)<>1 OR
 DB_NAME()<>N'Wayhunter_M5_Registry_IsolatedTest' THROW 58901,'Wrong phase/target',1;
IF ORIGINAL_LOGIN() COLLATE Latin1_General_100_BIN2 <> N'$(M5ApprovedExternalLogin)' COLLATE Latin1_General_100_BIN2
 OR ORIGINAL_LOGIN() COLLATE Latin1_General_100_BIN2=N'UNICOMTIC\ut011148' COLLATE Latin1_General_100_BIN2
 THROW 58902,'NOT a true external Windows login session',1;
IF ISNULL(IS_SRVROLEMEMBER(N'sysadmin'),1)<>0
 OR ISNULL(IS_ROLEMEMBER(N'db_owner'),1)<>0
 OR ISNULL(IS_ROLEMEMBER(N'db_ddladmin'),1)<>0
 THROW 58903,'External login is privileged; STOP',1;
DECLARE @cI int=HAS_PERMS_BY_NAME(N'dbo.GameProofMediaClaims',N'OBJECT',N'INSERT'),
        @cU int=HAS_PERMS_BY_NAME(N'dbo.GameProofMediaClaims',N'OBJECT',N'UPDATE'),
        @cD int=HAS_PERMS_BY_NAME(N'dbo.GameProofMediaClaims',N'OBJECT',N'DELETE'),
        @oI int=HAS_PERMS_BY_NAME(N'dbo.GameProofSubmissionOperations',N'OBJECT',N'INSERT'),
        @eU int=HAS_PERMS_BY_NAME(N'dbo.GameProofEvidence',N'OBJECT',N'UPDATE'),
        @execA int=HAS_PERMS_BY_NAME(N'dbo.M5Test_TXA_RegisterReserve',N'OBJECT',N'EXECUTE'),
        @execC int=HAS_PERMS_BY_NAME(N'dbo.M5Test_TXC_FinalizeProof',N'OBJECT',N'EXECUTE');
SELECT ORIGINAL_LOGIN() AS TrueOriginalLogin, SUSER_SNAME() AS SuserName,
       USER_NAME() AS DatabaseUser, @cI AS ClaimInsert,@cU AS ClaimUpdate,
       @cD AS ClaimDelete,@oI AS OperationInsert,@eU AS EvidenceUpdate,
       @execA AS ExecuteTXA,@execC AS ExecuteTXC;
IF COALESCE(@cI,-1)<>0 OR COALESCE(@cU,-1)<>0 OR COALESCE(@cD,-1)<>0
 OR COALESCE(@oI,-1)<>0 OR COALESCE(@eU,-1)<>0
 OR COALESCE(@execA,-1)<>1 OR COALESCE(@execC,-1)<>1
 THROW 58904,'Effective least-privilege test failed',1;
