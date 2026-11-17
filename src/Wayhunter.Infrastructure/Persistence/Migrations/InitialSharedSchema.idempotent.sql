IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE TABLE [Advertisements] (
        [Id] uniqueidentifier NOT NULL,
        [Title] nvarchar(200) NOT NULL,
        [ImageUrl] nvarchar(2048) NOT NULL,
        [DestinationUrl] nvarchar(2048) NULL,
        [StartAtUtc] datetimeoffset NOT NULL,
        [EndAtUtc] datetimeoffset NOT NULL,
        [IsEnabled] bit NOT NULL,
        [Placement] int NOT NULL,
        [Status] int NOT NULL,
        [ImpressionCount] bigint NOT NULL DEFAULT CAST(0 AS bigint),
        [ClickCount] bigint NOT NULL DEFAULT CAST(0 AS bigint),
        CONSTRAINT [PK_Advertisements] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Advertisements_Placement] CHECK ([Placement] IN (1, 2)),
        CONSTRAINT [CK_Advertisements_Status] CHECK ([Status] IN (0, 1, 2, 3)),
        CONSTRAINT [CK_Advertisements_ValidWindow] CHECK ([EndAtUtc] > [StartAtUtc])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE TABLE [CoinRuleVersions] (
        [Id] uniqueidentifier NOT NULL,
        [VersionCode] nvarchar(64) NOT NULL,
        [VoucherCostCoins] bigint NOT NULL,
        [VoucherReferenceLkr] decimal(18,2) NOT NULL,
        CONSTRAINT [PK_CoinRuleVersions] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_CoinRuleVersions_Values] CHECK ([VoucherCostCoins] > 0 AND [VoucherReferenceLkr] >= 0)
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE TABLE [SampleVoucherOffers] (
        [Id] uniqueidentifier NOT NULL,
        [OfferCode] nvarchar(100) NOT NULL,
        [CostCoins] bigint NOT NULL,
        [ReferenceValueLkr] decimal(18,2) NOT NULL,
        [IsActive] bit NOT NULL,
        [ValidFromUtc] datetimeoffset NOT NULL,
        [ValidUntilUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_SampleVoucherOffers] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_VoucherOffers_Values] CHECK ([CostCoins] > 0 AND [ReferenceValueLkr] >= 0),
        CONSTRAINT [CK_VoucherOffers_Window] CHECK ([ValidUntilUtc] > [ValidFromUtc])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE TABLE [Users] (
        [UserId] nvarchar(256) NOT NULL,
        [DisplayName] nvarchar(200) NOT NULL,
        [NormalizedEmail] nvarchar(320) NOT NULL,
        [PasswordHash] nvarchar(1024) NOT NULL,
        [Status] int NOT NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [VerifiedAtUtc] datetimeoffset NULL,
        [DeletionRequestedAtUtc] datetimeoffset NULL,
        CONSTRAINT [PK_Users] PRIMARY KEY ([UserId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE TABLE [CoinRewardOutbox] (
        [EventId] uniqueidentifier NOT NULL,
        [RewardType] int NOT NULL,
        [SourceRecordId] nvarchar(256) NOT NULL,
        [RecipientUserId] nvarchar(256) NOT NULL,
        [CoinsAmount] int NOT NULL,
        [EligibilityConfirmedAtUtc] datetimeoffset NOT NULL,
        [CorrelationId] uniqueidentifier NOT NULL,
        [RuleVersion] nvarchar(64) NOT NULL,
        [ApprovalRecordId] nvarchar(256) NULL,
        [GameAttemptId] nvarchar(256) NULL,
        [Status] int NOT NULL,
        [AttemptNumber] int NOT NULL,
        [NextDueAtUtc] datetimeoffset NOT NULL,
        [LeaseUntilUtc] datetimeoffset NULL,
        [ClaimToken] nvarchar(128) NULL,
        [LastErrorCode] nvarchar(128) NULL,
        [DeliveredAtUtc] datetimeoffset NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_CoinRewardOutbox] PRIMARY KEY ([EventId]),
        CONSTRAINT [CK_CoinRewardOutbox_Amount] CHECK ([CoinsAmount] > 0),
        CONSTRAINT [CK_CoinRewardOutbox_Attempts] CHECK ([AttemptNumber] >= 0),
        CONSTRAINT [CK_CoinRewardOutbox_Status] CHECK ([Status] IN (1, 2, 3)),
        CONSTRAINT [FK_CoinRewardOutbox_Users_RecipientUserId] FOREIGN KEY ([RecipientUserId]) REFERENCES [Users] ([UserId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE TABLE [CoinWallets] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] nvarchar(256) NOT NULL,
        [PostedCoins] bigint NOT NULL,
        [ReservedCoins] bigint NOT NULL,
        [LifetimeEarnedCoins] bigint NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_CoinWallets] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_CoinWallets_Id_UserId] UNIQUE ([Id], [UserId]),
        CONSTRAINT [CK_CoinWallets_Balances] CHECK ([PostedCoins] >= 0 AND [ReservedCoins] >= 0 AND [LifetimeEarnedCoins] >= 0 AND [ReservedCoins] <= [PostedCoins]),
        CONSTRAINT [FK_CoinWallets_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([UserId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE TABLE [Games] (
        [Id] uniqueidentifier NOT NULL,
        [CreatorUserId] nvarchar(256) NOT NULL,
        [Title] nvarchar(150) NULL,
        [Description] nvarchar(2000) NULL,
        [DurationMinutes] int NULL,
        [Status] int NOT NULL,
        [SubmittedAtUtc] datetimeoffset NULL,
        [ApprovedAtUtc] datetimeoffset NULL,
        [ExpiresAtUtc] datetimeoffset NULL,
        [IsFlaggedForReview] bit NOT NULL,
        [FlagReason] nvarchar(1000) NULL,
        [RejectionReason] nvarchar(1000) NULL,
        [DisabledReason] nvarchar(1000) NULL,
        [LastModeratedAtUtc] datetimeoffset NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Games] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Games_Duration] CHECK ([DurationMinutes] IS NULL OR [DurationMinutes] > 0),
        CONSTRAINT [CK_Games_ExpiryOrder] CHECK (([ApprovedAtUtc] IS NULL AND [ExpiresAtUtc] IS NULL) OR ([ApprovedAtUtc] IS NOT NULL AND [ExpiresAtUtc] IS NOT NULL AND [ExpiresAtUtc] = DATEADD(hour, 720, [ApprovedAtUtc]))),
        CONSTRAINT [CK_Games_Status] CHECK ([Status] IN (0,1,2,3,4,5)),
        CONSTRAINT [FK_Games_Users_CreatorUserId] FOREIGN KEY ([CreatorUserId]) REFERENCES [Users] ([UserId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE TABLE [Hunter_Submissions] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] nvarchar(256) NOT NULL,
        [UserNameSnapshot] nvarchar(200) NOT NULL,
        [PlaceName] nvarchar(200) NOT NULL,
        [Description] nvarchar(2000) NULL,
        [District] nvarchar(100) NOT NULL,
        [Latitude] float NOT NULL,
        [Longitude] float NOT NULL,
        [GpsAccuracyMeters] float NULL,
        [LocationCapturedAtUtc] datetimeoffset NULL,
        [Status] int NOT NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [SubmittedAtUtc] datetimeoffset NULL,
        CONSTRAINT [PK_Hunter_Submissions] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_Hunter_Submissions_Id_UserId] UNIQUE ([Id], [UserId]),
        CONSTRAINT [CK_Hunter_Submissions_GpsAccuracy] CHECK ([GpsAccuracyMeters] IS NULL OR [GpsAccuracyMeters] >= 0),
        CONSTRAINT [CK_Hunter_Submissions_Latitude] CHECK ([Latitude] BETWEEN -90 AND 90),
        CONSTRAINT [CK_Hunter_Submissions_Longitude] CHECK ([Longitude] BETWEEN -180 AND 180),
        CONSTRAINT [CK_Hunter_Submissions_Status] CHECK ([Status] IN (0, 1, 2, 3, 4)),
        CONSTRAINT [FK_Hunter_Submissions_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([UserId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE TABLE [IdentityConsents] (
        [ConsentId] nvarchar(36) NOT NULL,
        [UserId] nvarchar(256) NOT NULL,
        [ConsentKey] nvarchar(200) NOT NULL,
        [GrantedAtUtc] datetimeoffset NOT NULL,
        [RevokedAtUtc] datetimeoffset NULL,
        CONSTRAINT [PK_IdentityConsents] PRIMARY KEY ([ConsentId]),
        CONSTRAINT [FK_IdentityConsents_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([UserId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE TABLE [IdentityOtpChallenges] (
        [ChallengeId] nvarchar(36) NOT NULL,
        [UserId] nvarchar(256) NOT NULL,
        [OtpVerifierHash] nvarchar(1024) NOT NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [ExpiresAtUtc] datetimeoffset NOT NULL,
        [FailedAttemptCount] int NOT NULL,
        [MaxFailedAttempts] int NOT NULL,
        [ConsumedAtUtc] datetimeoffset NULL,
        [RevokedAtUtc] datetimeoffset NULL,
        CONSTRAINT [PK_IdentityOtpChallenges] PRIMARY KEY ([ChallengeId]),
        CONSTRAINT [FK_IdentityOtpChallenges_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([UserId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE TABLE [IdentitySessions] (
        [SessionId] nvarchar(36) NOT NULL,
        [UserId] nvarchar(256) NOT NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [ExpiresAtUtc] datetimeoffset NOT NULL,
        [RevokedAtUtc] datetimeoffset NULL,
        CONSTRAINT [PK_IdentitySessions] PRIMARY KEY ([SessionId]),
        CONSTRAINT [FK_IdentitySessions_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([UserId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE TABLE [Trips] (
        [Id] uniqueidentifier NOT NULL,
        [OwnerUserId] nvarchar(256) NOT NULL,
        [TripName] nvarchar(200) NOT NULL,
        [StartDate] date NOT NULL,
        [EndDate] date NOT NULL,
        [BudgetLkr] decimal(18,2) NOT NULL,
        [VehicleType] nvarchar(100) NOT NULL,
        [Headcount] int NOT NULL,
        [DraftStatus] int NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Trips] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Trips_Budget] CHECK ([BudgetLkr] >= 0),
        CONSTRAINT [CK_Trips_DateRange] CHECK ([EndDate] >= [StartDate]),
        CONSTRAINT [CK_Trips_Headcount] CHECK ([Headcount] >= 1),
        CONSTRAINT [FK_Trips_Users_OwnerUserId] FOREIGN KEY ([OwnerUserId]) REFERENCES [Users] ([UserId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE TABLE [TripSafety_HazardReports] (
        [Id] uniqueidentifier NOT NULL,
        [ReporterUserId] nvarchar(256) NOT NULL,
        [Status] int NOT NULL,
        [ReportedAtUtc] datetimeoffset NOT NULL,
        [PhotoCapturedAtUtc] datetimeoffset NOT NULL,
        [ExpiresAtUtc] datetimeoffset NOT NULL,
        [VerifiedAtUtc] datetimeoffset NULL,
        [ResolvedAtUtc] datetimeoffset NULL,
        [ExpiredAtUtc] datetimeoffset NULL,
        [VerifiedByUserId] nvarchar(256) NULL,
        [ResolvedByUserId] nvarchar(256) NULL,
        [ResolutionReason] nvarchar(max) NULL,
        CONSTRAINT [PK_TripSafety_HazardReports] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_TripSafety_HazardReports_ExpiredStatus] CHECK ([Status] <> 4 OR [ExpiredAtUtc] IS NOT NULL),
        CONSTRAINT [CK_TripSafety_HazardReports_Expiry] CHECK ([ExpiresAtUtc] = DATEADD(hour, 24, [ReportedAtUtc])),
        CONSTRAINT [CK_TripSafety_HazardReports_ExpiryTimestamp] CHECK ([ExpiredAtUtc] IS NULL OR [ExpiredAtUtc] >= [ExpiresAtUtc]),
        CONSTRAINT [CK_TripSafety_HazardReports_Resolution] CHECK (([ResolvedAtUtc] IS NULL AND [ResolvedByUserId] IS NULL AND [ResolutionReason] IS NULL) OR ([ResolvedAtUtc] IS NOT NULL AND [ResolvedByUserId] IS NOT NULL AND [ResolutionReason] IS NOT NULL AND [ResolvedAtUtc] >= [ReportedAtUtc])),
        CONSTRAINT [CK_TripSafety_HazardReports_ResolvedStatus] CHECK ([Status] <> 3 OR ([ResolvedAtUtc] IS NOT NULL AND [ResolvedByUserId] IS NOT NULL AND [ResolutionReason] IS NOT NULL)),
        CONSTRAINT [CK_TripSafety_HazardReports_Status] CHECK ([Status] IN (1, 2, 3, 4)),
        CONSTRAINT [CK_TripSafety_HazardReports_Verification] CHECK (([VerifiedAtUtc] IS NULL AND [VerifiedByUserId] IS NULL) OR ([VerifiedAtUtc] IS NOT NULL AND [VerifiedByUserId] IS NOT NULL AND [VerifiedAtUtc] >= [ReportedAtUtc])),
        CONSTRAINT [CK_TripSafety_HazardReports_VerifiedStatus] CHECK ([Status] <> 2 OR ([VerifiedAtUtc] IS NOT NULL AND [VerifiedByUserId] IS NOT NULL)),
        CONSTRAINT [FK_TripSafety_HazardReports_Users_ReporterUserId] FOREIGN KEY ([ReporterUserId]) REFERENCES [Users] ([UserId]),
        CONSTRAINT [FK_TripSafety_HazardReports_Users_ResolvedByUserId] FOREIGN KEY ([ResolvedByUserId]) REFERENCES [Users] ([UserId]),
        CONSTRAINT [FK_TripSafety_HazardReports_Users_VerifiedByUserId] FOREIGN KEY ([VerifiedByUserId]) REFERENCES [Users] ([UserId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE TABLE [UserProfiles] (
        [UserId] nvarchar(256) NOT NULL,
        [AvatarUrl] nvarchar(2048) NULL,
        [Bio] nvarchar(500) NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [UpdatedAtUtc] datetimeoffset NULL,
        CONSTRAINT [PK_UserProfiles] PRIMARY KEY ([UserId]),
        CONSTRAINT [FK_UserProfiles_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([UserId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE TABLE [CoinRewardConsumerDeliveries] (
        [Id] uniqueidentifier NOT NULL,
        [EventId] uniqueidentifier NOT NULL,
        [ConsumerName] nvarchar(100) NOT NULL,
        [Status] int NOT NULL,
        [AttemptCount] int NOT NULL,
        [DeliveredAtUtc] datetimeoffset NULL,
        [LastErrorCode] nvarchar(128) NULL,
        CONSTRAINT [PK_CoinRewardConsumerDeliveries] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_CoinRewardConsumerDeliveries_Attempts] CHECK ([AttemptCount] >= 0),
        CONSTRAINT [CK_CoinRewardConsumerDeliveries_Status] CHECK ([Status] IN (1, 2, 3)),
        CONSTRAINT [FK_CoinRewardConsumerDeliveries_CoinRewardOutbox_EventId] FOREIGN KEY ([EventId]) REFERENCES [CoinRewardOutbox] ([EventId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE TABLE [CoinLedgerEntries] (
        [Id] uniqueidentifier NOT NULL,
        [WalletId] uniqueidentifier NOT NULL,
        [DeltaCoins] bigint NOT NULL,
        [RewardType] int NULL,
        [SourceRecordId] nvarchar(256) NOT NULL,
        [RuleVersion] nvarchar(64) NOT NULL,
        [CorrelationId] uniqueidentifier NOT NULL,
        [RecordedAtUtc] datetimeoffset NOT NULL,
        [CorrectionOfEntryId] uniqueidentifier NULL,
        CONSTRAINT [PK_CoinLedgerEntries] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_CoinLedgerEntries_NonZero] CHECK ([DeltaCoins] <> 0),
        CONSTRAINT [FK_CoinLedgerEntries_CoinWallets_WalletId] FOREIGN KEY ([WalletId]) REFERENCES [CoinWallets] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE TABLE [SampleVoucherRedemptionRequests] (
        [Id] uniqueidentifier NOT NULL,
        [WalletId] uniqueidentifier NOT NULL,
        [OfferId] uniqueidentifier NOT NULL,
        [OwnerUserId] nvarchar(256) NOT NULL,
        [CostCoins] bigint NOT NULL,
        [Status] int NOT NULL,
        [AdminDecisionId] uniqueidentifier NULL,
        [RequestedAtUtc] datetimeoffset NOT NULL,
        [DecidedAtUtc] datetimeoffset NULL,
        CONSTRAINT [PK_SampleVoucherRedemptionRequests] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_VoucherRequests_Id_Wallet] UNIQUE ([Id], [WalletId]),
        CONSTRAINT [CK_VoucherRequests_Amount] CHECK ([CostCoins] > 0),
        CONSTRAINT [CK_VoucherRequests_Status] CHECK ([Status] IN (1, 2, 3, 4)),
        CONSTRAINT [FK_SampleVoucherRedemptionRequests_CoinWallets_WalletId_OwnerUserId] FOREIGN KEY ([WalletId], [OwnerUserId]) REFERENCES [CoinWallets] ([Id], [UserId]),
        CONSTRAINT [FK_SampleVoucherRedemptionRequests_SampleVoucherOffers_OfferId] FOREIGN KEY ([OfferId]) REFERENCES [SampleVoucherOffers] ([Id]),
        CONSTRAINT [FK_SampleVoucherRedemptionRequests_Users_OwnerUserId] FOREIGN KEY ([OwnerUserId]) REFERENCES [Users] ([UserId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE TABLE [GameAttempts] (
        [Id] uniqueidentifier NOT NULL,
        [GameId] uniqueidentifier NOT NULL,
        [PlayerUserId] nvarchar(256) NOT NULL,
        [StartedAtUtc] datetimeoffset NOT NULL,
        [DeadlineUtc] datetimeoffset NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_GameAttempts] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_GameAttempts_Id_Game] UNIQUE ([Id], [GameId]),
        CONSTRAINT [AK_GameAttempts_Id_Game_Player] UNIQUE ([Id], [GameId], [PlayerUserId]),
        CONSTRAINT [CK_GameAttempts_Deadline] CHECK ([DeadlineUtc] IS NULL OR [DeadlineUtc] > [StartedAtUtc]),
        CONSTRAINT [FK_GameAttempts_Games_GameId] FOREIGN KEY ([GameId]) REFERENCES [Games] ([Id]),
        CONSTRAINT [FK_GameAttempts_Users_PlayerUserId] FOREIGN KEY ([PlayerUserId]) REFERENCES [Users] ([UserId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE TABLE [GameCreationApprovals] (
        [ApprovalRecordId] nvarchar(256) NOT NULL,
        [GameId] uniqueidentifier NOT NULL,
        [AdminUserId] nvarchar(256) NOT NULL,
        [ApprovedAtUtc] datetimeoffset NOT NULL,
        [RewardEventId] uniqueidentifier NOT NULL,
        [CorrelationId] uniqueidentifier NOT NULL,
        [RewardRuleVersion] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_GameCreationApprovals] PRIMARY KEY ([ApprovalRecordId]),
        CONSTRAINT [FK_GameCreationApprovals_Games_GameId] FOREIGN KEY ([GameId]) REFERENCES [Games] ([Id]),
        CONSTRAINT [FK_GameCreationApprovals_Users_AdminUserId] FOREIGN KEY ([AdminUserId]) REFERENCES [Users] ([UserId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE TABLE [GameModerationDecisions] (
        [DecisionId] uniqueidentifier NOT NULL,
        [GameId] uniqueidentifier NOT NULL,
        [AdminUserId] nvarchar(256) NOT NULL,
        [Action] int NOT NULL,
        [Reason] nvarchar(1000) NOT NULL,
        [OccurredAtUtc] datetimeoffset NOT NULL,
        [ResultingStatus] int NOT NULL,
        [CorrelationId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_GameModerationDecisions] PRIMARY KEY ([DecisionId]),
        CONSTRAINT [CK_GameModeration_Action] CHECK ([Action] IN (1,2,3)),
        CONSTRAINT [CK_GameModeration_Status] CHECK ([ResultingStatus] IN (0,1,2,3,4,5)),
        CONSTRAINT [FK_GameModerationDecisions_Games_GameId] FOREIGN KEY ([GameId]) REFERENCES [Games] ([Id]),
        CONSTRAINT [FK_GameModerationDecisions_Users_AdminUserId] FOREIGN KEY ([AdminUserId]) REFERENCES [Users] ([UserId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE TABLE [GameSteps] (
        [Id] uniqueidentifier NOT NULL,
        [GameId] uniqueidentifier NOT NULL,
        [StepNumber] int NOT NULL,
        CONSTRAINT [PK_GameSteps] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_GameSteps_Game_StepNumber] UNIQUE ([GameId], [StepNumber]),
        CONSTRAINT [CK_GameSteps_Number] CHECK ([StepNumber] >= 1),
        CONSTRAINT [FK_GameSteps_Games_GameId] FOREIGN KEY ([GameId]) REFERENCES [Games] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE TABLE [Hunter_CaptureSessions] (
        [Id] uniqueidentifier NOT NULL,
        [HunterId] uniqueidentifier NOT NULL,
        [HunterSubmissionId] uniqueidentifier NOT NULL,
        [OwnerUserId] AS LOWER(CONVERT(nvarchar(256), [HunterId])) PERSISTED,
        [IssuedAtUtc] datetimeoffset NOT NULL,
        [ExpiresAtUtc] datetimeoffset NOT NULL,
        [IsConsumed] bit NOT NULL DEFAULT CAST(0 AS bit),
        [IsRevoked] bit NOT NULL DEFAULT CAST(0 AS bit),
        CONSTRAINT [PK_Hunter_CaptureSessions] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Hunter_CaptureSessions_TimeWindow] CHECK ([ExpiresAtUtc] > [IssuedAtUtc]),
        CONSTRAINT [FK_Hunter_CaptureSessions_Submission_Owner] FOREIGN KEY ([HunterSubmissionId], [OwnerUserId]) REFERENCES [Hunter_Submissions] ([Id], [UserId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE TABLE [Hunter_Evidence] (
        [Id] uniqueidentifier NOT NULL,
        [HunterSubmissionId] uniqueidentifier NOT NULL,
        [EvidenceType] int NOT NULL,
        [PrivateMediaReference] nvarchar(2048) NOT NULL,
        [CapturedAtUtc] datetimeoffset NOT NULL,
        [UploadedAtUtc] datetimeoffset NOT NULL,
        [CaptureLatitude] float NULL,
        [CaptureLongitude] float NULL,
        [DurationSeconds] float NULL,
        CONSTRAINT [PK_Hunter_Evidence] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Hunter_Evidence_CoordinatePair] CHECK (([CaptureLatitude] IS NULL AND [CaptureLongitude] IS NULL) OR ([CaptureLatitude] IS NOT NULL AND [CaptureLongitude] IS NOT NULL)),
        CONSTRAINT [CK_Hunter_Evidence_Duration] CHECK ([DurationSeconds] IS NULL OR [DurationSeconds] > 0),
        CONSTRAINT [CK_Hunter_Evidence_Type] CHECK ([EvidenceType] IN (1, 2)),
        CONSTRAINT [FK_Hunter_Evidence_Submissions] FOREIGN KEY ([HunterSubmissionId]) REFERENCES [Hunter_Submissions] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE TABLE [Hunter_ReviewDecisions] (
        [Id] uniqueidentifier NOT NULL,
        [HunterSubmissionId] uniqueidentifier NOT NULL,
        [ReviewerUserId] nvarchar(256) NOT NULL,
        [ResultingStatus] int NOT NULL,
        [Reason] nvarchar(2000) NULL,
        [ReviewedAtUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_Hunter_ReviewDecisions] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Hunter_ReviewDecisions_Status] CHECK ([ResultingStatus] IN (2, 3, 4)),
        CONSTRAINT [FK_Hunter_ReviewDecisions_Submissions] FOREIGN KEY ([HunterSubmissionId]) REFERENCES [Hunter_Submissions] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE TABLE [TripSafety_LiveTripSessions] (
        [SessionId] uniqueidentifier NOT NULL,
        [TripPlanId] uniqueidentifier NOT NULL,
        [Status] int NOT NULL,
        [StartedAtUtc] datetimeoffset NOT NULL,
        [FinishedAtUtc] datetimeoffset NULL,
        CONSTRAINT [PK_TripSafety_LiveTripSessions] PRIMARY KEY ([SessionId]),
        CONSTRAINT [CK_TripSafety_LiveTripSessions_Status] CHECK ([Status] IN (1, 2, 3)),
        CONSTRAINT [FK_TripSafety_LiveTripSessions_Trips_TripPlanId] FOREIGN KEY ([TripPlanId]) REFERENCES [Trips] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE TABLE [TripSafety_TripInvitations] (
        [Id] uniqueidentifier NOT NULL,
        [TripPlanId] uniqueidentifier NOT NULL,
        [SenderUserId] nvarchar(256) NOT NULL,
        [RecipientUserId] nvarchar(256) NOT NULL,
        [Status] int NOT NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [RespondedAtUtc] datetimeoffset NULL,
        CONSTRAINT [PK_TripSafety_TripInvitations] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_TripSafety_TripInvitations_ResponseTime] CHECK (([Status] = 0 AND [RespondedAtUtc] IS NULL) OR ([Status] IN (1, 2, 3) AND [RespondedAtUtc] IS NOT NULL AND [RespondedAtUtc] >= [CreatedAtUtc])),
        CONSTRAINT [CK_TripSafety_TripInvitations_Status] CHECK ([Status] IN (0, 1, 2, 3)),
        CONSTRAINT [FK_TripSafety_TripInvitations_Trips_TripPlanId] FOREIGN KEY ([TripPlanId]) REFERENCES [Trips] ([Id]),
        CONSTRAINT [FK_TripSafety_TripInvitations_Users_RecipientUserId] FOREIGN KEY ([RecipientUserId]) REFERENCES [Users] ([UserId]),
        CONSTRAINT [FK_TripSafety_TripInvitations_Users_SenderUserId] FOREIGN KEY ([SenderUserId]) REFERENCES [Users] ([UserId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE TABLE [TripStops] (
        [Id] uniqueidentifier NOT NULL,
        [TripPlanId] uniqueidentifier NOT NULL,
        [OrderIndex] int NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [PlaceId] nvarchar(256) NULL,
        [Latitude] float NULL,
        [Longitude] float NULL,
        CONSTRAINT [PK_TripStops] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_TripStops_Coordinates] CHECK (([Latitude] IS NULL AND [Longitude] IS NULL) OR ([Latitude] IS NOT NULL AND [Longitude] IS NOT NULL AND [Latitude] BETWEEN -90 AND 90 AND [Longitude] BETWEEN -180 AND 180)),
        CONSTRAINT [CK_TripStops_OrderIndex] CHECK ([OrderIndex] >= 0),
        CONSTRAINT [FK_TripStops_Trips_TripPlanId] FOREIGN KEY ([TripPlanId]) REFERENCES [Trips] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE TABLE [CoinReservations] (
        [Id] uniqueidentifier NOT NULL,
        [WalletId] uniqueidentifier NOT NULL,
        [RequestId] uniqueidentifier NOT NULL,
        [ReservedCoins] bigint NOT NULL,
        [Status] int NOT NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [ResolvedAtUtc] datetimeoffset NULL,
        CONSTRAINT [PK_CoinReservations] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_CoinReservations_Amount] CHECK ([ReservedCoins] > 0),
        CONSTRAINT [CK_CoinReservations_Status] CHECK ([Status] IN (1, 2, 3)),
        CONSTRAINT [FK_CoinReservations_CoinWallets_WalletId] FOREIGN KEY ([WalletId]) REFERENCES [CoinWallets] ([Id]),
        CONSTRAINT [FK_CoinReservations_SampleVoucherRedemptionRequests_RequestId_WalletId] FOREIGN KEY ([RequestId], [WalletId]) REFERENCES [SampleVoucherRedemptionRequests] ([Id], [WalletId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE TABLE [SampleVoucherInventory] (
        [Id] uniqueidentifier NOT NULL,
        [OfferId] uniqueidentifier NOT NULL,
        [CodeHash] nvarchar(128) NOT NULL,
        [EncryptedCodeReference] nvarchar(500) NOT NULL,
        [AssignedRequestId] uniqueidentifier NULL,
        CONSTRAINT [PK_SampleVoucherInventory] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SampleVoucherInventory_SampleVoucherOffers_OfferId] FOREIGN KEY ([OfferId]) REFERENCES [SampleVoucherOffers] ([Id]),
        CONSTRAINT [FK_SampleVoucherInventory_SampleVoucherRedemptionRequests_AssignedRequestId] FOREIGN KEY ([AssignedRequestId]) REFERENCES [SampleVoucherRedemptionRequests] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE TABLE [GameAttemptProgress] (
        [AttemptId] uniqueidentifier NOT NULL,
        [GameId] uniqueidentifier NOT NULL,
        [PlayerUserId] nvarchar(256) NOT NULL,
        [TotalSteps] int NOT NULL,
        [CurrentStepNumber] int NOT NULL,
        [RejectedEvidenceCount] int NOT NULL,
        [Status] int NOT NULL,
        CONSTRAINT [PK_GameAttemptProgress] PRIMARY KEY ([AttemptId]),
        CONSTRAINT [CK_GameProgress_Rejections] CHECK ([RejectedEvidenceCount] BETWEEN 0 AND 3),
        CONSTRAINT [CK_GameProgress_Status] CHECK ([Status] IN (0,1,2,3,4)),
        CONSTRAINT [CK_GameProgress_Steps] CHECK ([TotalSteps] > 0 AND [CurrentStepNumber] >= 1 AND [CurrentStepNumber] <= [TotalSteps]),
        CONSTRAINT [FK_GameAttemptProgress_GameAttempts_AttemptId_GameId_PlayerUserId] FOREIGN KEY ([AttemptId], [GameId], [PlayerUserId]) REFERENCES [GameAttempts] ([Id], [GameId], [PlayerUserId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE TABLE [GameCompletionResults] (
        [Id] uniqueidentifier NOT NULL,
        [GameId] uniqueidentifier NOT NULL,
        [AttemptId] uniqueidentifier NOT NULL,
        [PlayerUserId] nvarchar(256) NOT NULL,
        [AttemptStartedAtUtc] datetimeoffset NOT NULL,
        [AttemptDeadlineUtc] datetimeoffset NOT NULL,
        [CompletedAtUtc] datetimeoffset NOT NULL,
        [Status] int NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_GameCompletionResults] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_GameCompletionResults_Id_AttemptId] UNIQUE ([Id], [AttemptId]),
        CONSTRAINT [CK_GameCompletionResults_Status] CHECK ([Status] IN (0,1,2)),
        CONSTRAINT [CK_GameCompletionResults_Timeline] CHECK ([AttemptDeadlineUtc] > [AttemptStartedAtUtc] AND [CompletedAtUtc] >= [AttemptStartedAtUtc] AND [CompletedAtUtc] < [AttemptDeadlineUtc]),
        CONSTRAINT [FK_GameCompletionResults_GameAttempts_AttemptId_GameId_PlayerUserId] FOREIGN KEY ([AttemptId], [GameId], [PlayerUserId]) REFERENCES [GameAttempts] ([Id], [GameId], [PlayerUserId]),
        CONSTRAINT [FK_GameCompletionResults_Games_GameId] FOREIGN KEY ([GameId]) REFERENCES [Games] ([Id]),
        CONSTRAINT [FK_GameCompletionResults_Users_PlayerUserId] FOREIGN KEY ([PlayerUserId]) REFERENCES [Users] ([UserId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE TABLE [GameResultDecisions] (
        [DecisionId] uniqueidentifier NOT NULL,
        [GameId] uniqueidentifier NOT NULL,
        [AttemptId] uniqueidentifier NOT NULL,
        [AdminUserId] nvarchar(256) NOT NULL,
        [IsApproved] bit NOT NULL,
        [Reason] nvarchar(1000) NULL,
        [OccurredAtUtc] datetimeoffset NOT NULL,
        [CorrelationId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_GameResultDecisions] PRIMARY KEY ([DecisionId]),
        CONSTRAINT [FK_GameResultDecisions_GameAttempts_AttemptId_GameId] FOREIGN KEY ([AttemptId], [GameId]) REFERENCES [GameAttempts] ([Id], [GameId]),
        CONSTRAINT [FK_GameResultDecisions_Games_GameId] FOREIGN KEY ([GameId]) REFERENCES [Games] ([Id]),
        CONSTRAINT [FK_GameResultDecisions_Users_AdminUserId] FOREIGN KEY ([AdminUserId]) REFERENCES [Users] ([UserId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE TABLE [GameWinners] (
        [GameId] uniqueidentifier NOT NULL,
        [AttemptId] uniqueidentifier NOT NULL,
        [PlayerUserId] nvarchar(256) NOT NULL,
        [ApprovedAtUtc] datetimeoffset NOT NULL,
        [ApprovedByUserId] nvarchar(256) NOT NULL,
        CONSTRAINT [PK_GameWinners] PRIMARY KEY ([GameId]),
        CONSTRAINT [FK_GameWinners_GameAttempts_AttemptId_GameId_PlayerUserId] FOREIGN KEY ([AttemptId], [GameId], [PlayerUserId]) REFERENCES [GameAttempts] ([Id], [GameId], [PlayerUserId]),
        CONSTRAINT [FK_GameWinners_Games_GameId] FOREIGN KEY ([GameId]) REFERENCES [Games] ([Id]),
        CONSTRAINT [FK_GameWinners_Users_ApprovedByUserId] FOREIGN KEY ([ApprovedByUserId]) REFERENCES [Users] ([UserId]),
        CONSTRAINT [FK_GameWinners_Users_PlayerUserId] FOREIGN KEY ([PlayerUserId]) REFERENCES [Users] ([UserId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE TABLE [GameHints] (
        [Id] uniqueidentifier NOT NULL,
        [GameStepId] uniqueidentifier NOT NULL,
        [TextHint] nvarchar(2000) NULL,
        [PhotoMediaId] nvarchar(256) NULL,
        CONSTRAINT [PK_GameHints] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_GameHints_Content] CHECK ([TextHint] IS NOT NULL OR [PhotoMediaId] IS NOT NULL),
        CONSTRAINT [FK_GameHints_GameSteps_GameStepId] FOREIGN KEY ([GameStepId]) REFERENCES [GameSteps] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE TABLE [GameProofEvidence] (
        [Id] uniqueidentifier NOT NULL,
        [AttemptId] uniqueidentifier NOT NULL,
        [GameId] uniqueidentifier NOT NULL,
        [StepNumber] int NOT NULL,
        [MediaId] nvarchar(256) NOT NULL,
        [SubmittedAtUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_GameProofEvidence] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_GameProofEvidence_Id_Attempt] UNIQUE ([Id], [AttemptId]),
        CONSTRAINT [CK_GameProofEvidence_Step] CHECK ([StepNumber] >= 1),
        CONSTRAINT [FK_GameProofEvidence_GameAttempts_AttemptId_GameId] FOREIGN KEY ([AttemptId], [GameId]) REFERENCES [GameAttempts] ([Id], [GameId]),
        CONSTRAINT [FK_GameProofEvidence_GameSteps_GameId_StepNumber] FOREIGN KEY ([GameId], [StepNumber]) REFERENCES [GameSteps] ([GameId], [StepNumber])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE TABLE [TripSafety_GpsConsents] (
        [SessionId] uniqueidentifier NOT NULL,
        [UserId] nvarchar(256) NOT NULL,
        [IsGranted] bit NOT NULL,
        [GrantedAtUtc] datetimeoffset NULL,
        [RevokedAtUtc] datetimeoffset NULL,
        CONSTRAINT [PK_TripSafety_GpsConsents] PRIMARY KEY ([SessionId], [UserId]),
        CONSTRAINT [CK_TripSafety_GpsConsents_GrantState] CHECK (([IsGranted] = 0) OR ([IsGranted] = 1 AND [GrantedAtUtc] IS NOT NULL AND [RevokedAtUtc] IS NULL)),
        CONSTRAINT [CK_TripSafety_GpsConsents_RevocationTime] CHECK ([RevokedAtUtc] IS NULL OR ([GrantedAtUtc] IS NOT NULL AND [RevokedAtUtc] >= [GrantedAtUtc])),
        CONSTRAINT [FK_TripSafety_GpsConsents_LiveTripSessions_SessionId] FOREIGN KEY ([SessionId]) REFERENCES [TripSafety_LiveTripSessions] ([SessionId]),
        CONSTRAINT [FK_TripSafety_GpsConsents_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([UserId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE TABLE [TripSafety_TripMembers] (
        [Id] uniqueidentifier NOT NULL,
        [TripPlanId] uniqueidentifier NOT NULL,
        [MemberUserId] nvarchar(256) NOT NULL,
        [InvitationId] uniqueidentifier NOT NULL,
        [JoinedAtUtc] datetimeoffset NOT NULL,
        [IsActive] bit NOT NULL,
        [RemovedAtUtc] datetimeoffset NULL,
        CONSTRAINT [PK_TripSafety_TripMembers] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_TripSafety_TripMembers_RemovalState] CHECK (([IsActive] = 1 AND [RemovedAtUtc] IS NULL) OR ([IsActive] = 0 AND [RemovedAtUtc] IS NOT NULL AND [RemovedAtUtc] >= [JoinedAtUtc])),
        CONSTRAINT [FK_TripSafety_TripMembers_TripInvitations_InvitationId] FOREIGN KEY ([InvitationId]) REFERENCES [TripSafety_TripInvitations] ([Id]),
        CONSTRAINT [FK_TripSafety_TripMembers_Trips_TripPlanId] FOREIGN KEY ([TripPlanId]) REFERENCES [Trips] ([Id]),
        CONSTRAINT [FK_TripSafety_TripMembers_Users_MemberUserId] FOREIGN KEY ([MemberUserId]) REFERENCES [Users] ([UserId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE TABLE [SampleVoucherIssuances] (
        [Id] uniqueidentifier NOT NULL,
        [RequestId] uniqueidentifier NOT NULL,
        [InventoryId] uniqueidentifier NOT NULL,
        [Status] int NOT NULL,
        [QrPayload] nvarchar(2048) NULL,
        [ImageReference] nvarchar(2048) NULL,
        [IssuedAtUtc] datetimeoffset NULL,
        CONSTRAINT [PK_SampleVoucherIssuances] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_VoucherIssuances_Status] CHECK ([Status] IN (1, 2, 3)),
        CONSTRAINT [FK_SampleVoucherIssuances_SampleVoucherInventory_InventoryId] FOREIGN KEY ([InventoryId]) REFERENCES [SampleVoucherInventory] ([Id]),
        CONSTRAINT [FK_SampleVoucherIssuances_SampleVoucherRedemptionRequests_RequestId] FOREIGN KEY ([RequestId]) REFERENCES [SampleVoucherRedemptionRequests] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE TABLE [GameCompletionEvidence] (
        [CompletionResultId] uniqueidentifier NOT NULL,
        [EvidenceId] uniqueidentifier NOT NULL,
        [AttemptId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_GameCompletionEvidence] PRIMARY KEY ([CompletionResultId], [EvidenceId]),
        CONSTRAINT [FK_GameCompletionEvidence_GameCompletionResults_CompletionResultId_AttemptId] FOREIGN KEY ([CompletionResultId], [AttemptId]) REFERENCES [GameCompletionResults] ([Id], [AttemptId]),
        CONSTRAINT [FK_GameCompletionEvidence_GameProofEvidence_EvidenceId_AttemptId] FOREIGN KEY ([EvidenceId], [AttemptId]) REFERENCES [GameProofEvidence] ([Id], [AttemptId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE TABLE [GameProofReviewDecisions] (
        [DecisionId] uniqueidentifier NOT NULL,
        [EvidenceId] uniqueidentifier NOT NULL,
        [ReviewerUserId] nvarchar(256) NOT NULL,
        [IsApproved] bit NOT NULL,
        [Reason] nvarchar(1000) NULL,
        [ReviewedAtUtc] datetimeoffset NOT NULL,
        [CorrelationId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_GameProofReviewDecisions] PRIMARY KEY ([DecisionId]),
        CONSTRAINT [FK_GameProofReviewDecisions_GameProofEvidence_EvidenceId] FOREIGN KEY ([EvidenceId]) REFERENCES [GameProofEvidence] ([Id]),
        CONSTRAINT [FK_GameProofReviewDecisions_Users_ReviewerUserId] FOREIGN KEY ([ReviewerUserId]) REFERENCES [Users] ([UserId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE INDEX [IX_Advertisements_Enabled_Window] ON [Advertisements] ([IsEnabled], [StartAtUtc], [EndAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE INDEX [IX_CoinLedgerEntries_WalletId] ON [CoinLedgerEntries] ([WalletId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_CoinLedgerEntries_GameWinnerSource] ON [CoinLedgerEntries] ([RewardType], [SourceRecordId]) WHERE [RewardType] = 4');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_CoinLedgerEntries_Wallet_RewardSource] ON [CoinLedgerEntries] ([WalletId], [RewardType], [SourceRecordId]) WHERE [RewardType] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CoinReservations_RequestId_WalletId] ON [CoinReservations] ([RequestId], [WalletId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE INDEX [IX_CoinReservations_WalletId] ON [CoinReservations] ([WalletId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UX_CoinReservations_RequestId] ON [CoinReservations] ([RequestId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UX_CoinConsumer_Event_Name] ON [CoinRewardConsumerDeliveries] ([EventId], [ConsumerName]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE INDEX [IX_CoinOutbox_Due] ON [CoinRewardOutbox] ([Status], [NextDueAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE INDEX [IX_CoinRewardOutbox_RecipientUserId] ON [CoinRewardOutbox] ([RecipientUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_CoinOutbox_GameWinnerSource] ON [CoinRewardOutbox] ([RewardType], [SourceRecordId]) WHERE [RewardType] = 4');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UX_CoinOutbox_Recipient_RewardSource] ON [CoinRewardOutbox] ([RewardType], [SourceRecordId], [RecipientUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UX_CoinRuleVersions_VersionCode] ON [CoinRuleVersions] ([VersionCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UX_CoinWallets_UserId] ON [CoinWallets] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE INDEX [IX_GameAttemptProgress_AttemptId_GameId_PlayerUserId] ON [GameAttemptProgress] ([AttemptId], [GameId], [PlayerUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_GameProgress_Active_Game_Player] ON [GameAttemptProgress] ([GameId], [PlayerUserId]) WHERE [Status] IN (0,1,2)');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE INDEX [IX_GameAttempts_Game_Player] ON [GameAttempts] ([GameId], [PlayerUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE INDEX [IX_GameAttempts_PlayerUserId] ON [GameAttempts] ([PlayerUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE INDEX [IX_GameCompletionEvidence_CompletionResultId_AttemptId] ON [GameCompletionEvidence] ([CompletionResultId], [AttemptId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE INDEX [IX_GameCompletionEvidence_EvidenceId_AttemptId] ON [GameCompletionEvidence] ([EvidenceId], [AttemptId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UX_GameCompletionEvidence_Evidence] ON [GameCompletionEvidence] ([EvidenceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE INDEX [IX_GameCompletionResults_AttemptId_GameId_PlayerUserId] ON [GameCompletionResults] ([AttemptId], [GameId], [PlayerUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE INDEX [IX_GameCompletionResults_Game_Status_Time] ON [GameCompletionResults] ([GameId], [Status], [CompletedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE INDEX [IX_GameCompletionResults_PlayerUserId] ON [GameCompletionResults] ([PlayerUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UX_GameCompletionResults_Attempt] ON [GameCompletionResults] ([AttemptId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE INDEX [IX_GameCreationApprovals_AdminUserId] ON [GameCreationApprovals] ([AdminUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UX_GameCreationApprovals_Correlation] ON [GameCreationApprovals] ([CorrelationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UX_GameCreationApprovals_Game] ON [GameCreationApprovals] ([GameId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UX_GameCreationApprovals_RewardEvent] ON [GameCreationApprovals] ([RewardEventId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE INDEX [IX_GameHints_GameStepId] ON [GameHints] ([GameStepId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE INDEX [IX_GameModeration_Game_Time] ON [GameModerationDecisions] ([GameId], [OccurredAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE INDEX [IX_GameModerationDecisions_AdminUserId] ON [GameModerationDecisions] ([AdminUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UX_GameModeration_Correlation] ON [GameModerationDecisions] ([CorrelationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE INDEX [IX_GameProofEvidence_Attempt_Step_Time] ON [GameProofEvidence] ([AttemptId], [StepNumber], [SubmittedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE INDEX [IX_GameProofEvidence_AttemptId_GameId] ON [GameProofEvidence] ([AttemptId], [GameId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE INDEX [IX_GameProofEvidence_GameId_StepNumber] ON [GameProofEvidence] ([GameId], [StepNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE INDEX [IX_GameProofReviewDecisions_ReviewerUserId] ON [GameProofReviewDecisions] ([ReviewerUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UX_GameProofReviews_Correlation] ON [GameProofReviewDecisions] ([CorrelationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UX_GameProofReviews_Evidence] ON [GameProofReviewDecisions] ([EvidenceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE INDEX [IX_GameResultDecisions_AdminUserId] ON [GameResultDecisions] ([AdminUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE INDEX [IX_GameResultDecisions_AttemptId_GameId] ON [GameResultDecisions] ([AttemptId], [GameId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE INDEX [IX_GameResultDecisions_Game_Time] ON [GameResultDecisions] ([GameId], [OccurredAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UX_GameResults_Correlation] ON [GameResultDecisions] ([CorrelationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_GameResults_OneApprovedPerGame] ON [GameResultDecisions] ([GameId]) WHERE [IsApproved] = 1');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE INDEX [IX_Games_Creator_Submitted] ON [Games] ([CreatorUserId], [SubmittedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE INDEX [IX_Games_Status_Expiry] ON [Games] ([Status], [ExpiresAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UX_GameSteps_Game_Number] ON [GameSteps] ([GameId], [StepNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE INDEX [IX_GameWinners_ApprovedByUserId] ON [GameWinners] ([ApprovedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE INDEX [IX_GameWinners_AttemptId_GameId_PlayerUserId] ON [GameWinners] ([AttemptId], [GameId], [PlayerUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE INDEX [IX_GameWinners_PlayerUserId] ON [GameWinners] ([PlayerUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UX_GameWinners_Attempt] ON [GameWinners] ([AttemptId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE INDEX [IX_Hunter_CaptureSessions_Hunter_State] ON [Hunter_CaptureSessions] ([HunterId], [IsRevoked], [IsConsumed]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE INDEX [IX_Hunter_CaptureSessions_HunterSubmissionId_OwnerUserId] ON [Hunter_CaptureSessions] ([HunterSubmissionId], [OwnerUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE INDEX [IX_Hunter_CaptureSessions_Submission_State] ON [Hunter_CaptureSessions] ([HunterSubmissionId], [IsRevoked], [IsConsumed]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE INDEX [IX_Hunter_Evidence_Submission_Type] ON [Hunter_Evidence] ([HunterSubmissionId], [EvidenceType]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE INDEX [IX_Hunter_ReviewDecisions_Submission_Date] ON [Hunter_ReviewDecisions] ([HunterSubmissionId], [ReviewedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE INDEX [IX_Hunter_Submissions_District_Status] ON [Hunter_Submissions] ([District], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE INDEX [IX_Hunter_Submissions_UserId] ON [Hunter_Submissions] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE INDEX [IX_IdentityConsents_UserId_ConsentKey] ON [IdentityConsents] ([UserId], [ConsentKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE INDEX [IX_IdentityOtpChallenges_ExpiresAtUtc] ON [IdentityOtpChallenges] ([ExpiresAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE INDEX [IX_IdentityOtpChallenges_UserId] ON [IdentityOtpChallenges] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE INDEX [IX_IdentitySessions_ExpiresAtUtc] ON [IdentitySessions] ([ExpiresAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE INDEX [IX_IdentitySessions_UserId] ON [IdentitySessions] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE INDEX [IX_SampleVoucherInventory_OfferId] ON [SampleVoucherInventory] ([OfferId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_VoucherInventory_Assignment] ON [SampleVoucherInventory] ([AssignedRequestId]) WHERE [AssignedRequestId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UX_VoucherInventory_CodeHash] ON [SampleVoucherInventory] ([CodeHash]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UX_VoucherIssuances_Inventory] ON [SampleVoucherIssuances] ([InventoryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UX_VoucherIssuances_Request] ON [SampleVoucherIssuances] ([RequestId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UX_VoucherOffers_Code] ON [SampleVoucherOffers] ([OfferCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE INDEX [IX_SampleVoucherRedemptionRequests_OfferId] ON [SampleVoucherRedemptionRequests] ([OfferId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE INDEX [IX_SampleVoucherRedemptionRequests_OwnerUserId] ON [SampleVoucherRedemptionRequests] ([OwnerUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE INDEX [IX_SampleVoucherRedemptionRequests_WalletId_OwnerUserId] ON [SampleVoucherRedemptionRequests] ([WalletId], [OwnerUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE INDEX [IX_VoucherRequests_Wallet_Status] ON [SampleVoucherRedemptionRequests] ([WalletId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE INDEX [IX_Trips_OwnerUserId] ON [Trips] ([OwnerUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE INDEX [IX_TripSafety_GpsConsents_UserId] ON [TripSafety_GpsConsents] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE INDEX [IX_TripSafety_HazardReports_Reporter_ReportedAtUtc] ON [TripSafety_HazardReports] ([ReporterUserId], [ReportedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE INDEX [IX_TripSafety_HazardReports_ResolvedByUserId] ON [TripSafety_HazardReports] ([ResolvedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE INDEX [IX_TripSafety_HazardReports_Status_ExpiresAtUtc] ON [TripSafety_HazardReports] ([Status], [ExpiresAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE INDEX [IX_TripSafety_HazardReports_VerifiedByUserId] ON [TripSafety_HazardReports] ([VerifiedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_TripSafety_LiveTripSessions_OpenPlan] ON [TripSafety_LiveTripSessions] ([TripPlanId]) WHERE [Status] IN (1, 2)');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE INDEX [IX_TripSafety_TripInvitations_Recipient_Status] ON [TripSafety_TripInvitations] ([RecipientUserId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE INDEX [IX_TripSafety_TripInvitations_Sender_Created] ON [TripSafety_TripInvitations] ([SenderUserId], [CreatedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_TripSafety_TripInvitations_OpenRecipient] ON [TripSafety_TripInvitations] ([TripPlanId], [RecipientUserId]) WHERE [Status] IN (0, 1)');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE INDEX [IX_TripSafety_TripMembers_MemberUserId] ON [TripSafety_TripMembers] ([MemberUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE INDEX [IX_TripSafety_TripMembers_Trip_Active] ON [TripSafety_TripMembers] ([TripPlanId], [IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_TripSafety_TripMembers_ActiveTripUser] ON [TripSafety_TripMembers] ([TripPlanId], [MemberUserId]) WHERE [IsActive] = 1');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UX_TripSafety_TripMembers_InvitationId] ON [TripSafety_TripMembers] ([InvitationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TripStops_TripPlanId_OrderIndex] ON [TripStops] ([TripPlanId], [OrderIndex]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    CREATE UNIQUE INDEX [UX_Users_NormalizedEmail] ON [Users] ([NormalizedEmail]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261117093023_InitialSharedSchema'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261117093023_InitialSharedSchema', N'10.0.15');
END;

COMMIT;
GO

