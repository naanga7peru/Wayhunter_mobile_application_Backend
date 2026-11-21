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

IF OBJECT_ID(N'dbo.GameProofSubmissionOperations',N'U') IS NULL THROW 57020, 'Operation table missing', 1;
IF OBJECT_ID(N'dbo.TR_M5Test_Operation_Immutable',N'TR') IS NOT NULL THROW 57021, 'Trigger exists', 1;
GO
CREATE TRIGGER dbo.TR_M5Test_Operation_Immutable
ON dbo.GameProofSubmissionOperations AFTER UPDATE, DELETE AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i
               ON d.SubmissionOperationId=i.SubmissionOperationId
               WHERE i.SubmissionOperationId IS NULL)
        THROW 52101, 'Operation deletion/PK change forbidden', 1;
    IF EXISTS (SELECT 1 FROM inserted i LEFT JOIN deleted d
               ON d.SubmissionOperationId=i.SubmissionOperationId
               WHERE d.SubmissionOperationId IS NULL)
        THROW 52102, 'Operation PK change forbidden', 1;
    IF EXISTS (
        SELECT 1 FROM deleted d JOIN inserted i
          ON d.SubmissionOperationId=i.SubmissionOperationId
        WHERE d.GameId<>i.GameId OR d.AttemptId<>i.AttemptId
           OR d.StepNumber<>i.StepNumber OR d.CreatedAtUtc<>i.CreatedAtUtc
           OR DATALENGTH(d.AuthenticatedPlayerUserId) <> DATALENGTH(i.AuthenticatedPlayerUserId)
           OR CONVERT(varbinary(max),d.AuthenticatedPlayerUserId) <>
              CONVERT(varbinary(max),i.AuthenticatedPlayerUserId)
           OR DATALENGTH(d.CaptureSessionId) <> DATALENGTH(i.CaptureSessionId)
           OR CONVERT(varbinary(max),d.CaptureSessionId) <>
              CONVERT(varbinary(max),i.CaptureSessionId)
           OR DATALENGTH(d.MediaProviderNamespace) <> DATALENGTH(i.MediaProviderNamespace)
           OR CONVERT(varbinary(max),d.MediaProviderNamespace) <>
              CONVERT(varbinary(max),i.MediaProviderNamespace)
           OR DATALENGTH(d.CanonicalMediaId) <> DATALENGTH(i.CanonicalMediaId)
           OR CONVERT(varbinary(max),d.CanonicalMediaId) <>
              CONVERT(varbinary(max),i.CanonicalMediaId)
           OR (d.ProviderReceiptId IS NOT NULL AND
                (i.ProviderReceiptId IS NULL OR DATALENGTH(d.ProviderReceiptId) <> DATALENGTH(i.ProviderReceiptId) OR
                 CONVERT(varbinary(max),d.ProviderReceiptId) <>
                 CONVERT(varbinary(max),i.ProviderReceiptId)))
           OR (d.EvidenceId IS NOT NULL AND
                (i.EvidenceId IS NULL OR d.EvidenceId <> i.EvidenceId))
           OR (d.ProviderConsumptionConfirmedAtUtc IS NOT NULL AND
                (i.ProviderConsumptionConfirmedAtUtc IS NULL OR
                 d.ProviderConsumptionConfirmedAtUtc <> i.ProviderConsumptionConfirmedAtUtc))
           OR i.UpdatedAtUtc < d.UpdatedAtUtc
           OR NOT ((d.State=0 AND i.State=1)
                  OR (d.State=1 AND i.State IN (2,3))
                  OR (d.State=2 AND i.State=3)
                  OR (d.State=3 AND i.State=3)
                  OR (d.State=3 AND i.State=4))
           OR (i.State IN (3,4) AND (i.ProviderReceiptId IS NULL OR LTRIM(RTRIM(i.ProviderReceiptId))=N''))
           OR (i.State=4 AND i.EvidenceId IS NULL)
           -- 3 -> 3 is reserved exclusively for acquiring a fresh finalizer lease.
           OR (d.State=3 AND i.State=3 AND (
                 (d.ClaimToken IS NOT NULL AND d.LeaseExpiresAtUtc>TODATETIMEOFFSET(SYSUTCDATETIME(),N'+00:00'))
                 OR NULLIF(LTRIM(RTRIM(i.ClaimToken)),N'') IS NULL
                 OR i.LeaseExpiresAtUtc<=TODATETIMEOFFSET(SYSUTCDATETIME(),N'+00:00')
                 OR i.EvidenceId IS NOT NULL
                 OR i.ProviderReceiptId IS NULL
                 OR i.NextReconciliationAtUtc<>i.LeaseExpiresAtUtc
                 OR i.RecoveryAttemptCount<>d.RecoveryAttemptCount
                 OR ISNULL(i.LastErrorCode,N'')<>ISNULL(d.LastErrorCode,N'')
              ))
           -- 3 -> 4 requires an actively held finalizer token, consumed on completion.
           OR (d.State=3 AND i.State=4 AND (
                 NULLIF(LTRIM(RTRIM(d.ClaimToken)),N'') IS NULL
                 OR d.LeaseExpiresAtUtc<=TODATETIMEOFFSET(SYSUTCDATETIME(),N'+00:00')
                 OR i.ClaimToken IS NOT NULL OR i.EvidenceId IS NULL
                 OR i.NextReconciliationAtUtc IS NOT NULL
                 OR i.LeaseExpiresAtUtc>TODATETIMEOFFSET(SYSUTCDATETIME(),N'+00:00')
              ))
           OR (d.State=4) -- Completed immutable
    ) THROW 52103, 'Immutable operation value or completed row changed', 1;
END;
GO
