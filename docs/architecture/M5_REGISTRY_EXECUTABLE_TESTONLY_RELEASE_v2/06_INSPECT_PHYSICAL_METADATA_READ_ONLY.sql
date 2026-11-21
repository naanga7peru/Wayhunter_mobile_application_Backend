:ON ERROR EXIT
-- APPROVAL-GATED: Do not execute before exact written authorization for E_INSPECT.
-- Required sqlcmd -v M5AuthorizedPhase=E_INSPECT; variable is an accidental-run fence,
-- NOT itself authorization. Run via -S "(localdb)\MSSQLLocalDB" -E -b -X.
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF N'$(M5AuthorizedPhase)' <> N'E_INSPECT'
    THROW 57001, 'STOP: missing/wrong coordinator-approved phase', 1;
IF ISNULL(TRY_CONVERT(int,SERVERPROPERTY('IsLocalDB')),0) <> 1
    THROW 57002, 'STOP: not LocalDB', 1;
IF ORIGINAL_LOGIN() COLLATE Latin1_General_100_BIN2 <> N'UNICOMTIC\ut011148' COLLATE Latin1_General_100_BIN2
    THROW 57003, 'STOP: unexpected Windows login; coordinator must review', 1;
IF DB_NAME() <> N'Wayhunter_M5_Registry_IsolatedTest'
    THROW 57005, 'STOP: expected dedicated isolated database', 1;
IF DB_NAME() IN (N'master',N'model',N'msdb',N'tempdb')
    THROW 57006, 'STOP: system database', 1;
IF OBJECT_ID(N'dbo.M5RegistryFixtureManifest',N'U') IS NULL
    THROW 57007, 'STOP: missing M5 fixture manifest', 1;
IF NOT EXISTS (SELECT 1 FROM dbo.M5RegistryFixtureManifest
               WHERE RunId = CONVERT(uniqueidentifier,'F42D6B20-B250-4F93-AB07-0478C2FA8E85')
                 AND CreatedBy COLLATE Latin1_General_100_BIN2 = N'UNICOMTIC\ut011148' COLLATE Latin1_General_100_BIN2)
    THROW 57008, 'STOP: unrecognized fixture/owner', 1;

DECLARE @issues int=0;
IF (SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.GameProofMediaClaims'))<>16
    SET @issues+=1;
IF (SELECT COUNT(*) FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.GameProofMediaClaims'))<>5
    SET @issues+=1;
IF (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID(N'dbo.GameProofMediaClaims'))<>3
    SET @issues+=1;
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID(N'dbo.GameProofMediaClaims')
           AND (is_disabled=1 OR is_not_trusted=1)) SET @issues+=1;
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.GameProofMediaClaims')
           AND (is_disabled=1 OR is_not_trusted=1)) SET @issues+=1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id IN (OBJECT_ID(N'dbo.GameProofMediaClaims'),OBJECT_ID(N'dbo.GameProofSubmissionOperations'))
    AND name IN (N'MediaProviderNamespace',N'CanonicalMediaId') AND collation_name<>N'Latin1_General_100_BIN2')
    SET @issues+=1;
IF (SELECT COUNT(*) FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.GameProofMediaClaims')
    AND name=N'UX_GameProofMediaClaims_Provider_Canonical' AND is_unique=1 AND has_filter=0)<>1
    SET @issues+=1;
IF (SELECT COUNT(*) FROM sys.key_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.GameProofSubmissionOperations')
    AND name=N'AK_GameProofOperations_ImmutableClaimOwner')<>1 SET @issues+=1;
IF (SELECT COUNT(*) FROM sys.key_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.GameProofEvidence')
    AND name=N'AK_GameProofEvidence_Id_Game_Attempt_Step')<>1 SET @issues+=1;
IF EXISTS(SELECT 1 FROM sys.triggers WHERE parent_id IN
    (OBJECT_ID(N'dbo.GameProofMediaClaims'),OBJECT_ID(N'dbo.GameProofSubmissionOperations'),OBJECT_ID(N'dbo.GameProofEvidence'))
    AND is_disabled=1) SET @issues+=1;
SELECT @issues AS FailedMetadataAssertions,
       DB_NAME() AS VerifiedDatabase,ORIGINAL_LOGIN() AS ConnectedLogin,
       CAST(SERVERPROPERTY('ProductVersion') AS nvarchar(64)) AS SQLVersion,
       CAST(DATABASEPROPERTYEX(DB_NAME(),'Collation') AS nvarchar(128)) AS DatabaseCollation;
SELECT OBJECT_NAME(i.object_id) AS TableName,i.name AS IndexName,i.is_unique,i.type_desc,i.has_filter,i.filter_definition,
       SUM(CASE WHEN ic.key_ordinal>0 THEN c.max_length ELSE 0 END) AS DeclaredKeyBytes
FROM sys.indexes i
JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id
JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
WHERE i.object_id IN (OBJECT_ID(N'dbo.GameProofMediaClaims'),OBJECT_ID(N'dbo.GameProofSubmissionOperations'))
GROUP BY i.object_id,i.name,i.is_unique,i.type_desc,i.has_filter,i.filter_definition
ORDER BY TableName,IndexName;
SELECT OBJECT_NAME(parent_object_id) AS ChildTable,name AS FKName,delete_referential_action_desc,update_referential_action_desc,is_not_trusted
FROM sys.foreign_keys WHERE parent_object_id IN
 (OBJECT_ID(N'dbo.GameProofMediaClaims'),OBJECT_ID(N'dbo.GameProofSubmissionOperations'));
IF @issues<>0 THROW 57251,'STOP: physical metadata inspection mismatches proposal',1;
