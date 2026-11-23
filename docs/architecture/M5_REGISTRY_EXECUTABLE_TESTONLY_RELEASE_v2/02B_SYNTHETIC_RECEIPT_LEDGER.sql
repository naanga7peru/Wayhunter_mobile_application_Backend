-- C_SCHEMA candidate, no execution authorization. Must install only in an approved disposable M5 DB.
:ON ERROR EXIT
SET XACT_ABORT ON;
IF N'$(M5AuthorizedPhase)'<>N'C_SCHEMA'
 OR ISNULL(TRY_CONVERT(int,SERVERPROPERTY('IsLocalDB')),0)<>1
 OR DB_NAME()<>N'Wayhunter_M5_Registry_IsolatedTest'
 OR ORIGINAL_LOGIN() COLLATE Latin1_General_100_BIN2<>N'UNICOMTIC\ut011148' COLLATE Latin1_General_100_BIN2
 OR NOT EXISTS(SELECT 1 FROM dbo.M5RegistryFixtureManifest
 WHERE RunId='F42D6B20-B250-4F93-AB07-0478C2FA8E85')
    THROW 58401,'C_SCHEMA guard failed',1;
IF OBJECT_ID(N'dbo.M5TestSyntheticConsumedReceipts',N'U') IS NOT NULL
    THROW 58402,'Synthetic receipt table already exists',1;
IF OBJECT_ID(N'dbo.GameProofSubmissionOperations',N'U') IS NULL
    THROW 58403,'Operations schema prerequisite missing',1;
CREATE TABLE dbo.M5TestSyntheticConsumedReceipts(
    ProviderReceiptId nvarchar(256) COLLATE Latin1_General_100_BIN2 NOT NULL,
    SubmissionOperationId uniqueidentifier NOT NULL,
    GameId uniqueidentifier NOT NULL,
    AttemptId uniqueidentifier NOT NULL,
    StepNumber int NOT NULL,
    AuthenticatedPlayerUserId nvarchar(256) NOT NULL,
    CaptureSessionId nvarchar(256) NOT NULL,
    MediaProviderNamespace nvarchar(64) COLLATE Latin1_General_100_BIN2 NOT NULL,
    CanonicalMediaId nvarchar(256) COLLATE Latin1_General_100_BIN2 NOT NULL,
    IsFakeConfirmedConsumed bit NOT NULL,
    ObservedAtUtc datetimeoffset(7) NOT NULL,
    CONSTRAINT PK_M5TestSyntheticConsumedReceipts PRIMARY KEY(ProviderReceiptId),
    CONSTRAINT UX_M5TestSyntheticConsumedReceipts_Operation UNIQUE(SubmissionOperationId),
    CONSTRAINT CK_M5TestSyntheticReceipts_Confirmed CHECK(IsFakeConfirmedConsumed=1),
    CONSTRAINT FK_M5TestSyntheticReceipt_Operation FOREIGN KEY
     (SubmissionOperationId,GameId,AttemptId,AuthenticatedPlayerUserId,StepNumber,MediaProviderNamespace,CanonicalMediaId)
     REFERENCES dbo.GameProofSubmissionOperations
     (SubmissionOperationId,GameId,AttemptId,AuthenticatedPlayerUserId,StepNumber,MediaProviderNamespace,CanonicalMediaId)
     ON DELETE NO ACTION ON UPDATE NO ACTION
);
-- Only an approved fixture owner may INSERT a fake receipt. Grant no direct DML to executor.
