:ON ERROR EXIT
-- APPROVAL-GATED: Do not execute before exact written authorization for D_SECURITY.
-- Required sqlcmd -v M5AuthorizedPhase=D_SECURITY; variable is an accidental-run fence,
-- NOT itself authorization. Run via -S "(localdb)\MSSQLLocalDB" -E -b -X.
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF N'$(M5AuthorizedPhase)' <> N'D_SECURITY'
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

-- Loginless fixture principal: tests effective access using EXECUTE AS USER.
-- Real connection-principal privileges MUST be separately verified in phase E.
IF DATABASE_PRINCIPAL_ID(N'm5_registry_test_executor') IS NOT NULL
 OR DATABASE_PRINCIPAL_ID(N'm5_registry_test_probe') IS NOT NULL
    THROW 57201,'Existing principal; do not reuse',1;
IF (SELECT COUNT(*) FROM sys.procedures WHERE name IN
 (N'M5Test_TXA_RegisterReserve',N'M5Test_ClaimForFakeProvider',N'M5Test_TXB_ConfirmConsumption',
 N'M5Test_TXC_FinalizeProof',N'M5Test_ReconcileReadOnly',N'M5Test_ClaimFinalizer')) <>6
    THROW 57202,'Incomplete reviewed procedure set',1;
IF (SELECT COUNT(*) FROM sys.triggers WHERE name IN
 (N'TR_M5Test_GameProofMediaClaims_Immutable',N'TR_M5Test_Operation_Immutable',N'TR_M5Test_Evidence_AppendOnly')) <>3
    THROW 57203,'Missing immutable trigger',1;
CREATE ROLE [m5_registry_test_executor];
CREATE USER [m5_registry_test_probe] WITHOUT LOGIN;
ALTER ROLE [m5_registry_test_executor] ADD MEMBER [m5_registry_test_probe];
DENY INSERT,UPDATE,DELETE ON OBJECT::dbo.GameProofMediaClaims TO [m5_registry_test_executor];
DENY INSERT,UPDATE,DELETE ON OBJECT::dbo.GameProofSubmissionOperations TO [m5_registry_test_executor];
DENY INSERT,UPDATE,DELETE ON OBJECT::dbo.GameProofEvidence TO [m5_registry_test_executor];
DENY INSERT,UPDATE,DELETE ON OBJECT::dbo.M5TestSyntheticConsumedReceipts TO [m5_registry_test_executor];
GRANT EXECUTE ON OBJECT::dbo.M5Test_TXA_RegisterReserve TO [m5_registry_test_executor];
GRANT EXECUTE ON OBJECT::dbo.M5Test_ClaimForFakeProvider TO [m5_registry_test_executor];
GRANT EXECUTE ON OBJECT::dbo.M5Test_TXB_ConfirmConsumption TO [m5_registry_test_executor];
GRANT EXECUTE ON OBJECT::dbo.M5Test_TXC_FinalizeProof TO [m5_registry_test_executor];
GRANT EXECUTE ON OBJECT::dbo.M5Test_ReconcileReadOnly TO [m5_registry_test_executor];
GRANT EXECUTE ON OBJECT::dbo.M5Test_ClaimFinalizer TO [m5_registry_test_executor];
GO
:ON ERROR EXIT
-- APPROVAL-GATED: Do not execute before exact written authorization for D_SECURITY.
-- Required sqlcmd -v M5AuthorizedPhase=D_SECURITY; variable is an accidental-run fence,
-- NOT itself authorization. Run via -S "(localdb)\MSSQLLocalDB" -E -b -X.
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF N'$(M5AuthorizedPhase)' <> N'D_SECURITY'
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

-- Read-only permission probe under controlled impersonation.
BEGIN TRY
    EXECUTE AS USER=N'm5_registry_test_probe';
    SELECT USER_NAME() AS EffectiveUser,
       HAS_PERMS_BY_NAME(N'dbo.GameProofMediaClaims',N'OBJECT',N'INSERT') AS ClaimInsert,
       HAS_PERMS_BY_NAME(N'dbo.GameProofMediaClaims',N'OBJECT',N'UPDATE') AS ClaimUpdate,
       HAS_PERMS_BY_NAME(N'dbo.GameProofMediaClaims',N'OBJECT',N'DELETE') AS ClaimDelete,
       HAS_PERMS_BY_NAME(N'dbo.GameProofSubmissionOperations',N'OBJECT',N'INSERT') AS OperationInsert,
       HAS_PERMS_BY_NAME(N'dbo.GameProofEvidence',N'OBJECT',N'UPDATE') AS EvidenceUpdate,
       HAS_PERMS_BY_NAME(N'dbo.M5Test_TXA_RegisterReserve',N'OBJECT',N'EXECUTE') AS ExecuteTXA;
    IF HAS_PERMS_BY_NAME(N'dbo.GameProofMediaClaims',N'OBJECT',N'INSERT')<>0
       OR HAS_PERMS_BY_NAME(N'dbo.GameProofMediaClaims',N'OBJECT',N'UPDATE')<>0
       OR HAS_PERMS_BY_NAME(N'dbo.GameProofMediaClaims',N'OBJECT',N'DELETE')<>0
       OR HAS_PERMS_BY_NAME(N'dbo.M5Test_TXA_RegisterReserve',N'OBJECT',N'EXECUTE')<>1
       THROW 57204,'Least privilege probe failed',1;
    REVERT;
END TRY
BEGIN CATCH
    IF USER_NAME()=N'm5_registry_test_probe' REVERT;
    THROW;
END CATCH;
