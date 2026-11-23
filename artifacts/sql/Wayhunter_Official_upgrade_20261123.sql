BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123012139_ReconcileSharedModulePersistence'
)
BEGIN
    DROP INDEX [UX_VoucherIssuances_Inventory] ON [SampleVoucherIssuances];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123012139_ReconcileSharedModulePersistence'
)
BEGIN
    ALTER TABLE [SampleVoucherIssuances] DROP CONSTRAINT [CK_VoucherIssuances_Status];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123012139_ReconcileSharedModulePersistence'
)
BEGIN
    ALTER TABLE [TripSafety_HazardReports] ADD [Description] nvarchar(1000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123012139_ReconcileSharedModulePersistence'
)
BEGIN
    ALTER TABLE [TripSafety_HazardReports] ADD [Latitude] float NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123012139_ReconcileSharedModulePersistence'
)
BEGIN
    ALTER TABLE [TripSafety_HazardReports] ADD [Longitude] float NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123012139_ReconcileSharedModulePersistence'
)
BEGIN
    ALTER TABLE [TripSafety_HazardReports] ADD [PhotoAssetReference] nvarchar(512) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123012139_ReconcileSharedModulePersistence'
)
BEGIN
    ALTER TABLE [SampleVoucherRedemptionRequests] ADD [DecisionActorUserId] nvarchar(256) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123012139_ReconcileSharedModulePersistence'
)
BEGIN
    ALTER TABLE [SampleVoucherRedemptionRequests] ADD [DecisionReason] nvarchar(1000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123012139_ReconcileSharedModulePersistence'
)
BEGIN
    ALTER TABLE [SampleVoucherRedemptionRequests] ADD [OwnerNameSnapshot] nvarchar(200) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123012139_ReconcileSharedModulePersistence'
)
BEGIN
    DECLARE @var nvarchar(max);
    SELECT @var = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[SampleVoucherIssuances]') AND [c].[name] = N'InventoryId');
    IF @var IS NOT NULL EXEC(N'ALTER TABLE [SampleVoucherIssuances] DROP CONSTRAINT ' + @var + ';');
    ALTER TABLE [SampleVoucherIssuances] ALTER COLUMN [InventoryId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123012139_ReconcileSharedModulePersistence'
)
BEGIN
    ALTER TABLE [SampleVoucherIssuances] ADD [AttemptCount] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123012139_ReconcileSharedModulePersistence'
)
BEGIN
    ALTER TABLE [SampleVoucherIssuances] ADD [ExpiresAtUtc] datetimeoffset NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123012139_ReconcileSharedModulePersistence'
)
BEGIN
    ALTER TABLE [SampleVoucherIssuances] ADD [LastErrorCode] nvarchar(128) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123012139_ReconcileSharedModulePersistence'
)
BEGIN
    ALTER TABLE [SampleVoucherIssuances] ADD [RowVersion] rowversion NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123012139_ReconcileSharedModulePersistence'
)
BEGIN
    ALTER TABLE [SampleVoucherIssuances] ADD [UseReason] nvarchar(1000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123012139_ReconcileSharedModulePersistence'
)
BEGIN
    ALTER TABLE [SampleVoucherIssuances] ADD [UsedAtUtc] datetimeoffset NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123012139_ReconcileSharedModulePersistence'
)
BEGIN
    ALTER TABLE [SampleVoucherIssuances] ADD [UsedByActorUserId] nvarchar(256) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123012139_ReconcileSharedModulePersistence'
)
BEGIN
    ALTER TABLE [SampleVoucherInventory] ADD [AddedAtUtc] datetimeoffset NOT NULL DEFAULT '0001-01-01T00:00:00.0000000+00:00';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123012139_ReconcileSharedModulePersistence'
)
BEGIN
    ALTER TABLE [SampleVoucherInventory] ADD [AddedByAdminUserId] nvarchar(256) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123012139_ReconcileSharedModulePersistence'
)
BEGIN
    ALTER TABLE [SampleVoucherInventory] ADD [ExpiresAtUtc] datetimeoffset NOT NULL DEFAULT '0001-01-01T00:00:00.0000000+00:00';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123012139_ReconcileSharedModulePersistence'
)
BEGIN
    ALTER TABLE [SampleVoucherInventory] ADD [RowVersion] rowversion NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123012139_ReconcileSharedModulePersistence'
)
BEGIN
    ALTER TABLE [SampleVoucherInventory] ADD [SampleImageReference] nvarchar(2048) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123012139_ReconcileSharedModulePersistence'
)
BEGIN
    ALTER TABLE [CoinRuleVersions] ADD [CreatedAtUtc] datetimeoffset NOT NULL DEFAULT '0001-01-01T00:00:00.0000000+00:00';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123012139_ReconcileSharedModulePersistence'
)
BEGIN
    ALTER TABLE [CoinRuleVersions] ADD [CreatedByUserId] nvarchar(256) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123012139_ReconcileSharedModulePersistence'
)
BEGIN
    ALTER TABLE [CoinRuleVersions] ADD [EffectiveFromUtc] datetimeoffset NOT NULL DEFAULT '0001-01-01T00:00:00.0000000+00:00';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123012139_ReconcileSharedModulePersistence'
)
BEGIN
    ALTER TABLE [CoinRuleVersions] ADD [EffectiveUntilUtc] datetimeoffset NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123012139_ReconcileSharedModulePersistence'
)
BEGIN
    ALTER TABLE [CoinLedgerEntries] ADD [ActorUserId] nvarchar(256) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123012139_ReconcileSharedModulePersistence'
)
BEGIN
    ALTER TABLE [CoinLedgerEntries] ADD [Reason] nvarchar(1000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123012139_ReconcileSharedModulePersistence'
)
BEGIN
    ALTER TABLE [TripStops] ADD CONSTRAINT [AK_TripStops_Id_TripPlanId] UNIQUE ([Id], [TripPlanId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123012139_ReconcileSharedModulePersistence'
)
BEGIN
    ALTER TABLE [TripSafety_LiveTripSessions] ADD CONSTRAINT [AK_TripSafety_LiveTripSessions_SessionId_TripPlanId] UNIQUE ([SessionId], [TripPlanId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123012139_ReconcileSharedModulePersistence'
)
BEGIN
    CREATE TABLE [TripPlanCopyAttributions] (
        [TripPlanId] uniqueidentifier NOT NULL,
        [SourceTripPlanId] uniqueidentifier NOT NULL,
        [SourceOwnerUserId] nvarchar(256) NOT NULL,
        [CopiedAtUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_TripPlanCopyAttributions] PRIMARY KEY ([TripPlanId]),
        CONSTRAINT [CK_TripPlanCopies_NotOwnSource] CHECK ([TripPlanId] <> [SourceTripPlanId]),
        CONSTRAINT [FK_TripPlanCopyAttributions_Trips_TripPlanId] FOREIGN KEY ([TripPlanId]) REFERENCES [Trips] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123012139_ReconcileSharedModulePersistence'
)
BEGIN
    CREATE TABLE [TripSafety_MemberRevocationOutbox] (
        [Id] uniqueidentifier NOT NULL,
        [TripPlanId] uniqueidentifier NOT NULL,
        [MemberId] uniqueidentifier NOT NULL,
        [MemberUserId] nvarchar(256) NOT NULL,
        [RemovedAtUtc] datetimeoffset NOT NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [IsDelivered] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeliveredAtUtc] datetimeoffset NULL,
        [DeliveryAttempts] int NOT NULL DEFAULT 0,
        [NextAttemptAtUtc] datetimeoffset NULL,
        [LeaseToken] uniqueidentifier NULL,
        [LeaseUntilUtc] datetimeoffset NULL,
        [DeadLetteredAtUtc] datetimeoffset NULL,
        [LastError] nvarchar(256) NULL,
        CONSTRAINT [PK_TripSafety_MemberRevocationOutbox] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_TripSafety_MemberRevocationOutbox_Attempts] CHECK ([DeliveryAttempts] >= 0),
        CONSTRAINT [CK_TripSafety_MemberRevocationOutbox_DeliveryState] CHECK (([IsDelivered] = 0 AND [DeliveredAtUtc] IS NULL) OR ([IsDelivered] = 1 AND [DeliveredAtUtc] IS NOT NULL)),
        CONSTRAINT [FK_TripSafety_MemberRevocationOutbox_TripSafety_TripMembers_MemberId] FOREIGN KEY ([MemberId]) REFERENCES [TripSafety_TripMembers] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123012139_ReconcileSharedModulePersistence'
)
BEGIN
    CREATE TABLE [TripSafety_NotificationAudienceReceipts] (
        [ReceiptId] uniqueidentifier NOT NULL,
        [ActorId] nvarchar(256) NOT NULL,
        [Fingerprint] varchar(64) NOT NULL,
        [Count] int NOT NULL,
        [ExpiresAtUtc] datetimeoffset NOT NULL,
        [ConsumedAtUtc] datetimeoffset NULL,
        CONSTRAINT [PK_TripSafety_NotificationAudienceReceipts] PRIMARY KEY ([ReceiptId]),
        CONSTRAINT [CK_MS08_NotificationAudience_Count] CHECK ([Count] BETWEEN 1 AND 100),
        CONSTRAINT [FK_TripSafety_NotificationAudienceReceipts_Users_ActorId] FOREIGN KEY ([ActorId]) REFERENCES [Users] ([UserId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123012139_ReconcileSharedModulePersistence'
)
BEGIN
    CREATE TABLE [TripSafety_NotificationDevices] (
        [DeviceId] uniqueidentifier NOT NULL,
        [UserId] nvarchar(256) NOT NULL,
        [TokenFingerprint] varchar(64) NOT NULL,
        [ProtectedToken] varbinary(max) NOT NULL,
        [PushAllowed] bit NOT NULL,
        [ChangedAtUtc] datetimeoffset NOT NULL,
        [RevokedAtUtc] datetimeoffset NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_TripSafety_NotificationDevices] PRIMARY KEY ([DeviceId]),
        CONSTRAINT [FK_TripSafety_NotificationDevices_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([UserId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123012139_ReconcileSharedModulePersistence'
)
BEGIN
    CREATE TABLE [TripSafety_NotificationJobs] (
        [JobId] uniqueidentifier NOT NULL,
        [ActorId] nvarchar(256) NOT NULL,
        [Kind] int NOT NULL,
        [State] int NOT NULL,
        [Revision] int NOT NULL,
        [DueAtUtc] datetimeoffset NOT NULL,
        [Template] int NOT NULL,
        [TemplateVersion] int NOT NULL,
        [TargetId] uniqueidentifier NULL,
        [TripPlanId] uniqueidentifier NULL,
        [SessionId] uniqueidentifier NULL,
        [PlannedStartDate] date NULL,
        [TripVersion] varchar(16) NULL,
        [AudienceRole] int NULL,
        [AudienceDistrict] nvarchar(40) NULL,
        [AudienceFingerprint] varchar(64) NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_TripSafety_NotificationJobs] PRIMARY KEY ([JobId]),
        CONSTRAINT [CK_MS08_NotificationJob_Kind] CHECK (([Kind] = 1 AND [TripPlanId] IS NOT NULL AND [SessionId] IS NOT NULL AND [TripVersion] IS NOT NULL AND [PlannedStartDate] IS NOT NULL) OR ([Kind] = 2 AND [TripPlanId] IS NULL AND [SessionId] IS NULL AND [TripVersion] IS NULL AND [PlannedStartDate] IS NULL AND [AudienceFingerprint] IS NOT NULL)),
        CONSTRAINT [CK_MS08_NotificationJob_State] CHECK ([State] BETWEEN 0 AND 2 AND [Revision] > 0),
        CONSTRAINT [FK_TripSafety_NotificationJobs_TripSafety_LiveTripSessions_SessionId_TripPlanId] FOREIGN KEY ([SessionId], [TripPlanId]) REFERENCES [TripSafety_LiveTripSessions] ([SessionId], [TripPlanId]),
        CONSTRAINT [FK_TripSafety_NotificationJobs_Trips_TripPlanId] FOREIGN KEY ([TripPlanId]) REFERENCES [Trips] ([Id]),
        CONSTRAINT [FK_TripSafety_NotificationJobs_Users_ActorId] FOREIGN KEY ([ActorId]) REFERENCES [Users] ([UserId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123012139_ReconcileSharedModulePersistence'
)
BEGIN
    CREATE TABLE [TripSafety_NotificationOperations] (
        [ActorId] nvarchar(256) NOT NULL,
        [OperationId] uniqueidentifier NOT NULL,
        [ResourceId] uniqueidentifier NOT NULL,
        [Action] varchar(32) NOT NULL,
        [Fingerprint] varchar(64) NOT NULL,
        [JobState] int NULL,
        [Revision] int NULL,
        [DueAtUtc] datetimeoffset NULL,
        [RecordedAtUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_TripSafety_NotificationOperations] PRIMARY KEY ([ActorId], [OperationId]),
        CONSTRAINT [FK_TripSafety_NotificationOperations_Users_ActorId] FOREIGN KEY ([ActorId]) REFERENCES [Users] ([UserId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123012139_ReconcileSharedModulePersistence'
)
BEGIN
    CREATE TABLE [TripSafety_NotificationPreferences] (
        [UserId] nvarchar(256) NOT NULL,
        [Category] int NOT NULL,
        [PushEnabled] bit NOT NULL,
        CONSTRAINT [PK_TripSafety_NotificationPreferences] PRIMARY KEY ([UserId], [Category]),
        CONSTRAINT [CK_MS08_NotificationPreference_Category] CHECK ([Category] BETWEEN 1 AND 7),
        CONSTRAINT [FK_TripSafety_NotificationPreferences_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([UserId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123012139_ReconcileSharedModulePersistence'
)
BEGIN
    CREATE TABLE [TripSafety_SafeCheckIns] (
        [CheckInId] uniqueidentifier NOT NULL,
        [TripPlanId] uniqueidentifier NOT NULL,
        [SessionId] uniqueidentifier NOT NULL,
        [UserId] nvarchar(256) NOT NULL,
        [CheckedInAtUtc] datetimeoffset(7) NOT NULL,
        CONSTRAINT [PK_TripSafety_SafeCheckIns] PRIMARY KEY ([CheckInId]),
        CONSTRAINT [FK_TripSafety_SafeCheckIns_LiveTripSessions_SessionId_TripPlanId] FOREIGN KEY ([SessionId], [TripPlanId]) REFERENCES [TripSafety_LiveTripSessions] ([SessionId], [TripPlanId]),
        CONSTRAINT [FK_TripSafety_SafeCheckIns_Trips_TripPlanId] FOREIGN KEY ([TripPlanId]) REFERENCES [Trips] ([Id]),
        CONSTRAINT [FK_TripSafety_SafeCheckIns_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([UserId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123012139_ReconcileSharedModulePersistence'
)
BEGIN
    CREATE TABLE [TripSafety_TripMemories] (
        [MemoryId] uniqueidentifier NOT NULL,
        [TripPlanId] uniqueidentifier NOT NULL,
        [TripStopId] uniqueidentifier NULL,
        [OwnerUserId] nvarchar(256) NOT NULL,
        [CreateOperationId] uniqueidentifier NOT NULL,
        [Caption] nvarchar(1000) NOT NULL,
        [Visibility] int NOT NULL DEFAULT 0,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [UpdatedAtUtc] datetimeoffset NOT NULL,
        [DeletedAtUtc] datetimeoffset NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_TripSafety_TripMemories] PRIMARY KEY ([MemoryId]),
        CONSTRAINT [CK_TripSafety_TripMemories_Timestamps] CHECK ([UpdatedAtUtc] >= [CreatedAtUtc] AND ([DeletedAtUtc] IS NULL OR [DeletedAtUtc] >= [CreatedAtUtc])),
        CONSTRAINT [CK_TripSafety_TripMemories_Visibility] CHECK ([Visibility] IN (0, 1)),
        CONSTRAINT [FK_TripSafety_TripMemories_TripStops_TripStopId_TripPlanId] FOREIGN KEY ([TripStopId], [TripPlanId]) REFERENCES [TripStops] ([Id], [TripPlanId]),
        CONSTRAINT [FK_TripSafety_TripMemories_Trips_TripPlanId] FOREIGN KEY ([TripPlanId]) REFERENCES [Trips] ([Id]),
        CONSTRAINT [FK_TripSafety_TripMemories_Users_OwnerUserId] FOREIGN KEY ([OwnerUserId]) REFERENCES [Users] ([UserId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123012139_ReconcileSharedModulePersistence'
)
BEGIN
    CREATE TABLE [UserRoleAssignments] (
        [AssignmentId] uniqueidentifier NOT NULL,
        [UserId] nvarchar(256) NOT NULL,
        [Role] nvarchar(64) NOT NULL,
        [Status] nvarchar(16) NOT NULL,
        [AssignedAtUtc] datetimeoffset NOT NULL,
        [AssignedByUserId] nvarchar(256) NULL,
        [RevokedAtUtc] datetimeoffset NULL,
        [RevokedByUserId] nvarchar(256) NULL,
        CONSTRAINT [PK_UserRoleAssignments] PRIMARY KEY ([AssignmentId]),
        CONSTRAINT [CK_UserRoleAssignments_AssignmentTime] CHECK ([AssignedAtUtc] IS NOT NULL),
        CONSTRAINT [CK_UserRoleAssignments_Revocation] CHECK ((
                        ([Status] = N'Active'
                            AND [RevokedAtUtc] IS NULL
                            AND [RevokedByUserId] IS NULL)
                        OR
                        ([Status] = N'Inactive'
                            AND [RevokedAtUtc] IS NULL
                            AND [RevokedByUserId] IS NULL)
                        OR
                        ([Status] = N'Revoked'
                            AND [RevokedAtUtc] IS NOT NULL
                            AND [RevokedAtUtc] >= [AssignedAtUtc])
                    )),
        CONSTRAINT [CK_UserRoleAssignments_Role] CHECK ([Role] = N'Admin'),
        CONSTRAINT [CK_UserRoleAssignments_Status] CHECK ([Status] IN (N'Active', N'Revoked', N'Inactive')),
        CONSTRAINT [FK_UserRoleAssignments_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([UserId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123012139_ReconcileSharedModulePersistence'
)
BEGIN
    CREATE TABLE [TripSafety_NotificationTokenBindings] (
        [Fingerprint] varchar(64) NOT NULL,
        [DeviceId] uniqueidentifier NOT NULL,
        [UserId] nvarchar(256) NOT NULL,
        [RecordedAtUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_TripSafety_NotificationTokenBindings] PRIMARY KEY ([Fingerprint]),
        CONSTRAINT [FK_TripSafety_NotificationTokenBindings_TripSafety_NotificationDevices_DeviceId] FOREIGN KEY ([DeviceId]) REFERENCES [TripSafety_NotificationDevices] ([DeviceId]),
        CONSTRAINT [FK_TripSafety_NotificationTokenBindings_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([UserId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123012139_ReconcileSharedModulePersistence'
)
BEGIN
    CREATE TABLE [TripSafety_NotificationEvents] (
        [EventId] uniqueidentifier NOT NULL,
        [Category] int NOT NULL,
        [Template] int NOT NULL,
        [TemplateVersion] int NOT NULL,
        [TargetId] uniqueidentifier NULL,
        [JobId] uniqueidentifier NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_TripSafety_NotificationEvents] PRIMARY KEY ([EventId]),
        CONSTRAINT [CK_MS08_NotificationEvent_Template] CHECK ([Category] BETWEEN 1 AND 7 AND [Template] BETWEEN 1 AND 7 AND [TemplateVersion] > 0),
        CONSTRAINT [FK_TripSafety_NotificationEvents_TripSafety_NotificationJobs_JobId] FOREIGN KEY ([JobId]) REFERENCES [TripSafety_NotificationJobs] ([JobId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123012139_ReconcileSharedModulePersistence'
)
BEGIN
    CREATE TABLE [TripSafety_NotificationJobRecipients] (
        [JobId] uniqueidentifier NOT NULL,
        [UserId] nvarchar(256) NOT NULL,
        CONSTRAINT [PK_TripSafety_NotificationJobRecipients] PRIMARY KEY ([JobId], [UserId]),
        CONSTRAINT [FK_TripSafety_NotificationJobRecipients_TripSafety_NotificationJobs_JobId] FOREIGN KEY ([JobId]) REFERENCES [TripSafety_NotificationJobs] ([JobId]),
        CONSTRAINT [FK_TripSafety_NotificationJobRecipients_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([UserId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123012139_ReconcileSharedModulePersistence'
)
BEGIN
    CREATE TABLE [TripSafety_SafeCheckInOutbox] (
        [Id] uniqueidentifier NOT NULL,
        [CheckInId] uniqueidentifier NOT NULL,
        [RecipientUserId] nvarchar(256) NOT NULL,
        [CreatedAtUtc] datetimeoffset(7) NOT NULL,
        [DeliveryAttempts] int NOT NULL DEFAULT 0,
        [IsDelivered] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeliveredAtUtc] datetimeoffset(7) NULL,
        [NextAttemptAtUtc] datetimeoffset(7) NULL,
        [LeaseToken] uniqueidentifier NULL,
        [LeaseUntilUtc] datetimeoffset(7) NULL,
        [DeadLetteredAtUtc] datetimeoffset(7) NULL,
        [LastError] nvarchar(256) NULL,
        CONSTRAINT [PK_TripSafety_SafeCheckInOutbox] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_TripSafety_SafeCheckInOutbox_Attempts] CHECK ([DeliveryAttempts] >= 0),
        CONSTRAINT [CK_TripSafety_SafeCheckInOutbox_DeliveryState] CHECK (([IsDelivered] = 0 AND [DeliveredAtUtc] IS NULL) OR ([IsDelivered] = 1 AND [DeliveredAtUtc] IS NOT NULL AND [DeadLetteredAtUtc] IS NULL)),
        CONSTRAINT [CK_TripSafety_SafeCheckInOutbox_LeasePair] CHECK (([LeaseToken] IS NULL AND [LeaseUntilUtc] IS NULL) OR ([LeaseToken] IS NOT NULL AND [LeaseUntilUtc] IS NOT NULL)),
        CONSTRAINT [CK_TripSafety_SafeCheckInOutbox_TerminalState] CHECK (([IsDelivered] = 0 AND [DeadLetteredAtUtc] IS NULL) OR ([LeaseToken] IS NULL AND [LeaseUntilUtc] IS NULL AND [NextAttemptAtUtc] IS NULL)),
        CONSTRAINT [FK_TripSafety_SafeCheckInOutbox_SafeCheckIns_CheckInId] FOREIGN KEY ([CheckInId]) REFERENCES [TripSafety_SafeCheckIns] ([CheckInId]),
        CONSTRAINT [FK_TripSafety_SafeCheckInOutbox_Users_RecipientUserId] FOREIGN KEY ([RecipientUserId]) REFERENCES [Users] ([UserId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123012139_ReconcileSharedModulePersistence'
)
BEGIN
    CREATE TABLE [TripSafety_TripMemoryMedia] (
        [OperationId] uniqueidentifier NOT NULL,
        [MemoryId] uniqueidentifier NOT NULL,
        [ContentType] nvarchar(64) NOT NULL,
        [ContentLength] bigint NOT NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [State] int NOT NULL,
        [ObjectKey] nvarchar(450) NULL,
        [ObjectVersion] nvarchar(256) NULL,
        [CleanupClaimId] uniqueidentifier NULL,
        [CleanedAtUtc] datetimeoffset NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_TripSafety_TripMemoryMedia] PRIMARY KEY ([OperationId]),
        CONSTRAINT [CK_TripSafety_TripMemoryMedia_Size] CHECK ([ContentLength] > 0),
        CONSTRAINT [CK_TripSafety_TripMemoryMedia_State] CHECK (([State] = 0 AND [ObjectKey] IS NULL AND [ObjectVersion] IS NULL AND [CleanupClaimId] IS NULL AND [CleanedAtUtc] IS NULL) OR ([State] = 1 AND [ObjectKey] IS NOT NULL AND [ObjectVersion] IS NOT NULL AND [CleanupClaimId] IS NULL AND [CleanedAtUtc] IS NULL) OR ([State] IN (2, 3) AND [ObjectKey] IS NOT NULL AND [ObjectVersion] IS NOT NULL AND [CleanupClaimId] IS NOT NULL AND (([State] = 2 AND [CleanedAtUtc] IS NULL) OR ([State] = 3 AND [CleanedAtUtc] IS NOT NULL)))),
        CONSTRAINT [FK_TripSafety_TripMemoryMedia_TripSafety_TripMemories_MemoryId] FOREIGN KEY ([MemoryId]) REFERENCES [TripSafety_TripMemories] ([MemoryId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123012139_ReconcileSharedModulePersistence'
)
BEGIN
    CREATE TABLE [TripSafety_NotificationRecipients] (
        [EventId] uniqueidentifier NOT NULL,
        [UserId] nvarchar(256) NOT NULL,
        [ReadAtUtc] datetimeoffset NULL,
        [State] int NOT NULL,
        [Attempts] int NOT NULL,
        [NextAttemptAtUtc] datetimeoffset NULL,
        [LeaseToken] uniqueidentifier NULL,
        [LeaseUntilUtc] datetimeoffset NULL,
        [CompletedAtUtc] datetimeoffset NULL,
        [LastOutcome] nvarchar(48) NULL,
        CONSTRAINT [PK_TripSafety_NotificationRecipients] PRIMARY KEY ([EventId], [UserId]),
        CONSTRAINT [CK_MS08_NotificationRecipient_Lease] CHECK (([LeaseToken] IS NULL AND [LeaseUntilUtc] IS NULL) OR ([State] = 0 AND [LeaseToken] IS NOT NULL AND [LeaseUntilUtc] IS NOT NULL)),
        CONSTRAINT [CK_MS08_NotificationRecipient_State] CHECK ([Attempts] >= 0 AND (([State] = 0 AND [CompletedAtUtc] IS NULL) OR ([State] BETWEEN 1 AND 3 AND [CompletedAtUtc] IS NOT NULL AND [NextAttemptAtUtc] IS NULL))),
        CONSTRAINT [FK_TripSafety_NotificationRecipients_TripSafety_NotificationEvents_EventId] FOREIGN KEY ([EventId]) REFERENCES [TripSafety_NotificationEvents] ([EventId]),
        CONSTRAINT [FK_TripSafety_NotificationRecipients_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([UserId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123012139_ReconcileSharedModulePersistence'
)
BEGIN
    CREATE TABLE [TripSafety_NotificationDeliveryAudit] (
        [AuditId] uniqueidentifier NOT NULL,
        [EventId] uniqueidentifier NOT NULL,
        [UserId] nvarchar(256) NOT NULL,
        [Attempt] int NOT NULL,
        [Outcome] varchar(48) NOT NULL,
        [RecordedAtUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_TripSafety_NotificationDeliveryAudit] PRIMARY KEY ([AuditId]),
        CONSTRAINT [FK_TripSafety_NotificationDeliveryAudit_TripSafety_NotificationRecipients_EventId_UserId] FOREIGN KEY ([EventId], [UserId]) REFERENCES [TripSafety_NotificationRecipients] ([EventId], [UserId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123012139_ReconcileSharedModulePersistence'
)
BEGIN
    EXEC(N'ALTER TABLE [TripSafety_HazardReports] ADD CONSTRAINT [CK_TripSafety_HazardReports_Coordinates] CHECK (([Latitude] IS NULL AND [Longitude] IS NULL) OR ([Latitude] BETWEEN -90 AND 90 AND [Longitude] BETWEEN -180 AND 180))');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123012139_ReconcileSharedModulePersistence'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_VoucherIssuances_Inventory] ON [SampleVoucherIssuances] ([InventoryId]) WHERE [InventoryId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123012139_ReconcileSharedModulePersistence'
)
BEGIN
    EXEC(N'ALTER TABLE [SampleVoucherIssuances] ADD CONSTRAINT [CK_VoucherIssuances_Attempts] CHECK ([AttemptCount] >= 0)');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123012139_ReconcileSharedModulePersistence'
)
BEGIN
    EXEC(N'ALTER TABLE [SampleVoucherIssuances] ADD CONSTRAINT [CK_VoucherIssuances_Status] CHECK ([Status] IN (1, 2, 3, 4, 5))');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123012139_ReconcileSharedModulePersistence'
)
BEGIN
    EXEC(N'ALTER TABLE [SampleVoucherInventory] ADD CONSTRAINT [CK_VoucherInventory_Expiry] CHECK ([ExpiresAtUtc] > [AddedAtUtc])');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123012139_ReconcileSharedModulePersistence'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_CoinLedgerEntries_CorrectionOf] ON [CoinLedgerEntries] ([CorrectionOfEntryId]) WHERE [CorrectionOfEntryId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123012139_ReconcileSharedModulePersistence'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_CoinLedgerEntries_Wallet_NonRewardSource] ON [CoinLedgerEntries] ([WalletId], [SourceRecordId]) WHERE [RewardType] IS NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123012139_ReconcileSharedModulePersistence'
)
BEGIN
    CREATE INDEX [IX_TripPlanCopyAttributions_SourceTripPlanId] ON [TripPlanCopyAttributions] ([SourceTripPlanId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123012139_ReconcileSharedModulePersistence'
)
BEGIN
    CREATE INDEX [IX_TripSafety_MemberRevocationOutbox_Pending] ON [TripSafety_MemberRevocationOutbox] ([IsDelivered], [CreatedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123012139_ReconcileSharedModulePersistence'
)
BEGIN
    CREATE UNIQUE INDEX [UX_TripSafety_MemberRevocationOutbox_MemberId] ON [TripSafety_MemberRevocationOutbox] ([MemberId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123012139_ReconcileSharedModulePersistence'
)
BEGIN
    CREATE INDEX [IX_TripSafety_NotificationAudienceReceipts_ActorId] ON [TripSafety_NotificationAudienceReceipts] ([ActorId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123012139_ReconcileSharedModulePersistence'
)
BEGIN
    CREATE INDEX [IX_TripSafety_NotificationAudienceReceipts_ExpiresAtUtc] ON [TripSafety_NotificationAudienceReceipts] ([ExpiresAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123012139_ReconcileSharedModulePersistence'
)
BEGIN
    CREATE INDEX [IX_TripSafety_NotificationDeliveryAudit_EventId_UserId] ON [TripSafety_NotificationDeliveryAudit] ([EventId], [UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123012139_ReconcileSharedModulePersistence'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TripSafety_NotificationDevices_TokenFingerprint] ON [TripSafety_NotificationDevices] ([TokenFingerprint]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123012139_ReconcileSharedModulePersistence'
)
BEGIN
    CREATE INDEX [IX_TripSafety_NotificationDevices_UserId_RevokedAtUtc] ON [TripSafety_NotificationDevices] ([UserId], [RevokedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123012139_ReconcileSharedModulePersistence'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_TripSafety_NotificationEvents_JobId] ON [TripSafety_NotificationEvents] ([JobId]) WHERE [JobId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123012139_ReconcileSharedModulePersistence'
)
BEGIN
    CREATE INDEX [IX_TripSafety_NotificationJobRecipients_UserId] ON [TripSafety_NotificationJobRecipients] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123012139_ReconcileSharedModulePersistence'
)
BEGIN
    CREATE INDEX [IX_TripSafety_NotificationJobs_ActorId] ON [TripSafety_NotificationJobs] ([ActorId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123012139_ReconcileSharedModulePersistence'
)
BEGIN
    CREATE INDEX [IX_TripSafety_NotificationJobs_SessionId_TripPlanId] ON [TripSafety_NotificationJobs] ([SessionId], [TripPlanId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123012139_ReconcileSharedModulePersistence'
)
BEGIN
    CREATE INDEX [IX_TripSafety_NotificationJobs_State_DueAtUtc] ON [TripSafety_NotificationJobs] ([State], [DueAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123012139_ReconcileSharedModulePersistence'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_TripSafety_NotificationJobs_TripPlanId_SessionId_ActorId] ON [TripSafety_NotificationJobs] ([TripPlanId], [SessionId], [ActorId]) WHERE [Kind] = 1');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123012139_ReconcileSharedModulePersistence'
)
BEGIN
    CREATE INDEX [IX_TripSafety_NotificationOperations_ResourceId_RecordedAtUtc] ON [TripSafety_NotificationOperations] ([ResourceId], [RecordedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123012139_ReconcileSharedModulePersistence'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_TripSafety_NotificationRecipients_NextAttemptAtUtc_LeaseUntilUtc] ON [TripSafety_NotificationRecipients] ([NextAttemptAtUtc], [LeaseUntilUtc]) WHERE [State] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123012139_ReconcileSharedModulePersistence'
)
BEGIN
    CREATE INDEX [IX_TripSafety_NotificationRecipients_UserId_ReadAtUtc_EventId] ON [TripSafety_NotificationRecipients] ([UserId], [ReadAtUtc], [EventId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123012139_ReconcileSharedModulePersistence'
)
BEGIN
    CREATE INDEX [IX_TripSafety_NotificationTokenBindings_DeviceId] ON [TripSafety_NotificationTokenBindings] ([DeviceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123012139_ReconcileSharedModulePersistence'
)
BEGIN
    CREATE INDEX [IX_TripSafety_NotificationTokenBindings_UserId] ON [TripSafety_NotificationTokenBindings] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123012139_ReconcileSharedModulePersistence'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_TripSafety_SafeCheckInOutbox_PendingDue] ON [TripSafety_SafeCheckInOutbox] ([NextAttemptAtUtc], [CreatedAtUtc], [Id]) WHERE [IsDelivered] = 0 AND [DeadLetteredAtUtc] IS NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123012139_ReconcileSharedModulePersistence'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_TripSafety_SafeCheckInOutbox_PendingLeaseExpiry] ON [TripSafety_SafeCheckInOutbox] ([LeaseUntilUtc], [Id]) WHERE [IsDelivered] = 0 AND [DeadLetteredAtUtc] IS NULL AND [LeaseUntilUtc] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123012139_ReconcileSharedModulePersistence'
)
BEGIN
    CREATE INDEX [IX_TripSafety_SafeCheckInOutbox_RecipientUserId] ON [TripSafety_SafeCheckInOutbox] ([RecipientUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123012139_ReconcileSharedModulePersistence'
)
BEGIN
    CREATE UNIQUE INDEX [UX_TripSafety_SafeCheckInOutbox_CheckInId] ON [TripSafety_SafeCheckInOutbox] ([CheckInId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123012139_ReconcileSharedModulePersistence'
)
BEGIN
    CREATE INDEX [IX_TripSafety_SafeCheckIns_SessionId_TripPlanId] ON [TripSafety_SafeCheckIns] ([SessionId], [TripPlanId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123012139_ReconcileSharedModulePersistence'
)
BEGIN
    CREATE INDEX [IX_TripSafety_SafeCheckIns_TripPlanId] ON [TripSafety_SafeCheckIns] ([TripPlanId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123012139_ReconcileSharedModulePersistence'
)
BEGIN
    CREATE INDEX [IX_TripSafety_SafeCheckIns_UserId] ON [TripSafety_SafeCheckIns] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123012139_ReconcileSharedModulePersistence'
)
BEGIN
    CREATE UNIQUE INDEX [UX_TripSafety_SafeCheckIns_SessionId_UserId] ON [TripSafety_SafeCheckIns] ([SessionId], [UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123012139_ReconcileSharedModulePersistence'
)
BEGIN
    CREATE INDEX [IX_TripSafety_TripMemories_OwnerUserId] ON [TripSafety_TripMemories] ([OwnerUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123012139_ReconcileSharedModulePersistence'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_TripSafety_TripMemories_TripPlanId_OwnerUserId_CreatedAtUtc] ON [TripSafety_TripMemories] ([TripPlanId], [OwnerUserId], [CreatedAtUtc]) WHERE [DeletedAtUtc] IS NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123012139_ReconcileSharedModulePersistence'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TripSafety_TripMemories_TripPlanId_OwnerUserId_CreateOperationId] ON [TripSafety_TripMemories] ([TripPlanId], [OwnerUserId], [CreateOperationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123012139_ReconcileSharedModulePersistence'
)
BEGIN
    CREATE INDEX [IX_TripSafety_TripMemories_TripStopId_TripPlanId] ON [TripSafety_TripMemories] ([TripStopId], [TripPlanId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123012139_ReconcileSharedModulePersistence'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TripSafety_TripMemoryMedia_MemoryId] ON [TripSafety_TripMemoryMedia] ([MemoryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123012139_ReconcileSharedModulePersistence'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_TripSafety_TripMemoryMedia_ObjectKey] ON [TripSafety_TripMemoryMedia] ([ObjectKey]) WHERE [ObjectKey] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123012139_ReconcileSharedModulePersistence'
)
BEGIN
    CREATE INDEX [IX_TripSafety_TripMemoryMedia_State_CreatedAtUtc] ON [TripSafety_TripMemoryMedia] ([State], [CreatedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123012139_ReconcileSharedModulePersistence'
)
BEGIN
    CREATE INDEX [IX_UserRoleAssignments_AssignedAtUtc] ON [UserRoleAssignments] ([AssignedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123012139_ReconcileSharedModulePersistence'
)
BEGIN
    CREATE INDEX [IX_UserRoleAssignments_UserId_Status] ON [UserRoleAssignments] ([UserId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123012139_ReconcileSharedModulePersistence'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_UserRoleAssignments_Active_UserId_Role] ON [UserRoleAssignments] ([UserId], [Role]) WHERE [Status] = N''Active''');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261123012139_ReconcileSharedModulePersistence'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261123012139_ReconcileSharedModulePersistence', N'10.0.15');
END;

COMMIT;
GO

