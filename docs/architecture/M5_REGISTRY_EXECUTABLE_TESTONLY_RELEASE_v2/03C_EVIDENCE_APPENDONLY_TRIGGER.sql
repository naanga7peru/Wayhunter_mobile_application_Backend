:ON ERROR EXIT
-- APPROVAL-GATED: Do not execute before exact written authorization for D_GUARDS.
-- Required sqlcmd -v M5AuthorizedPhase=D_GUARDS; variable is an accidental-run fence,
-- NOT itself authorization. Run via -S "(localdb)\MSSQLLocalDB" -E -b -X.
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF N'$(M5AuthorizedPhase)' <> N'D_GUARDS'
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

IF OBJECT_ID(N'dbo.GameProofEvidence',N'U') IS NULL THROW 57020, 'Evidence table missing', 1;
IF OBJECT_ID(N'dbo.TR_M5Test_Evidence_AppendOnly',N'TR') IS NOT NULL THROW 57021, 'Trigger exists', 1;
GO
CREATE TRIGGER dbo.TR_M5Test_Evidence_AppendOnly
ON dbo.GameProofEvidence AFTER UPDATE, DELETE AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS(SELECT 1 FROM deleted)
        THROW 52104, 'Proof evidence append-only (no UPDATE/DELETE)', 1;
END;
GO
