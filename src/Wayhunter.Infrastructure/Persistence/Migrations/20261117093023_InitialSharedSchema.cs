using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayhunter.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSharedSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Advertisements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ImageUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    DestinationUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    StartAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EndAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    Placement = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ImpressionCount = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    ClickCount = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Advertisements", x => x.Id);
                    table.CheckConstraint("CK_Advertisements_Placement", "[Placement] IN (1, 2)");
                    table.CheckConstraint("CK_Advertisements_Status", "[Status] IN (0, 1, 2, 3)");
                    table.CheckConstraint("CK_Advertisements_ValidWindow", "[EndAtUtc] > [StartAtUtc]");
                });

            migrationBuilder.CreateTable(
                name: "CoinRuleVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    VoucherCostCoins = table.Column<long>(type: "bigint", nullable: false),
                    VoucherReferenceLkr = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoinRuleVersions", x => x.Id);
                    table.CheckConstraint("CK_CoinRuleVersions_Values", "[VoucherCostCoins] > 0 AND [VoucherReferenceLkr] >= 0");
                });

            migrationBuilder.CreateTable(
                name: "SampleVoucherOffers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OfferCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CostCoins = table.Column<long>(type: "bigint", nullable: false),
                    ReferenceValueLkr = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    ValidFromUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ValidUntilUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SampleVoucherOffers", x => x.Id);
                    table.CheckConstraint("CK_VoucherOffers_Values", "[CostCoins] > 0 AND [ReferenceValueLkr] >= 0");
                    table.CheckConstraint("CK_VoucherOffers_Window", "[ValidUntilUtc] > [ValidFromUtc]");
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    VerifiedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeletionRequestedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.UserId);
                });

            migrationBuilder.CreateTable(
                name: "CoinRewardOutbox",
                columns: table => new
                {
                    EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RewardType = table.Column<int>(type: "int", nullable: false),
                    SourceRecordId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    RecipientUserId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CoinsAmount = table.Column<int>(type: "int", nullable: false),
                    EligibilityConfirmedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RuleVersion = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ApprovalRecordId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    GameAttemptId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AttemptNumber = table.Column<int>(type: "int", nullable: false),
                    NextDueAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LeaseUntilUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ClaimToken = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    LastErrorCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    DeliveredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoinRewardOutbox", x => x.EventId);
                    table.CheckConstraint("CK_CoinRewardOutbox_Amount", "[CoinsAmount] > 0");
                    table.CheckConstraint("CK_CoinRewardOutbox_Attempts", "[AttemptNumber] >= 0");
                    table.CheckConstraint("CK_CoinRewardOutbox_Status", "[Status] IN (1, 2, 3)");
                    table.ForeignKey(
                        name: "FK_CoinRewardOutbox_Users_RecipientUserId",
                        column: x => x.RecipientUserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "CoinWallets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    PostedCoins = table.Column<long>(type: "bigint", nullable: false),
                    ReservedCoins = table.Column<long>(type: "bigint", nullable: false),
                    LifetimeEarnedCoins = table.Column<long>(type: "bigint", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoinWallets", x => x.Id);
                    table.UniqueConstraint("AK_CoinWallets_Id_UserId", x => new { x.Id, x.UserId });
                    table.CheckConstraint("CK_CoinWallets_Balances", "[PostedCoins] >= 0 AND [ReservedCoins] >= 0 AND [LifetimeEarnedCoins] >= 0 AND [ReservedCoins] <= [PostedCoins]");
                    table.ForeignKey(
                        name: "FK_CoinWallets_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "Games",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatorUserId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    DurationMinutes = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    SubmittedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ApprovedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsFlaggedForReview = table.Column<bool>(type: "bit", nullable: false),
                    FlagReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DisabledReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    LastModeratedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Games", x => x.Id);
                    table.CheckConstraint("CK_Games_Duration", "[DurationMinutes] IS NULL OR [DurationMinutes] > 0");
                    table.CheckConstraint("CK_Games_ExpiryOrder", "([ApprovedAtUtc] IS NULL AND [ExpiresAtUtc] IS NULL) OR ([ApprovedAtUtc] IS NOT NULL AND [ExpiresAtUtc] IS NOT NULL AND [ExpiresAtUtc] = DATEADD(hour, 720, [ApprovedAtUtc]))");
                    table.CheckConstraint("CK_Games_Status", "[Status] IN (0,1,2,3,4,5)");
                    table.ForeignKey(
                        name: "FK_Games_Users_CreatorUserId",
                        column: x => x.CreatorUserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "Hunter_Submissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    UserNameSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PlaceName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    District = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Latitude = table.Column<double>(type: "float", nullable: false),
                    Longitude = table.Column<double>(type: "float", nullable: false),
                    GpsAccuracyMeters = table.Column<double>(type: "float", nullable: true),
                    LocationCapturedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    SubmittedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Hunter_Submissions", x => x.Id);
                    table.UniqueConstraint("AK_Hunter_Submissions_Id_UserId", x => new { x.Id, x.UserId });
                    table.CheckConstraint("CK_Hunter_Submissions_GpsAccuracy", "[GpsAccuracyMeters] IS NULL OR [GpsAccuracyMeters] >= 0");
                    table.CheckConstraint("CK_Hunter_Submissions_Latitude", "[Latitude] BETWEEN -90 AND 90");
                    table.CheckConstraint("CK_Hunter_Submissions_Longitude", "[Longitude] BETWEEN -180 AND 180");
                    table.CheckConstraint("CK_Hunter_Submissions_Status", "[Status] IN (0, 1, 2, 3, 4)");
                    table.ForeignKey(
                        name: "FK_Hunter_Submissions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "IdentityConsents",
                columns: table => new
                {
                    ConsentId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ConsentKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    GrantedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RevokedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdentityConsents", x => x.ConsentId);
                    table.ForeignKey(
                        name: "FK_IdentityConsents_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "IdentityOtpChallenges",
                columns: table => new
                {
                    ChallengeId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    OtpVerifierHash = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    FailedAttemptCount = table.Column<int>(type: "int", nullable: false),
                    MaxFailedAttempts = table.Column<int>(type: "int", nullable: false),
                    ConsumedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RevokedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdentityOtpChallenges", x => x.ChallengeId);
                    table.ForeignKey(
                        name: "FK_IdentityOtpChallenges_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "IdentitySessions",
                columns: table => new
                {
                    SessionId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RevokedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdentitySessions", x => x.SessionId);
                    table.ForeignKey(
                        name: "FK_IdentitySessions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "Trips",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerUserId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    TripName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    BudgetLkr = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    VehicleType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Headcount = table.Column<int>(type: "int", nullable: false),
                    DraftStatus = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Trips", x => x.Id);
                    table.CheckConstraint("CK_Trips_Budget", "[BudgetLkr] >= 0");
                    table.CheckConstraint("CK_Trips_DateRange", "[EndDate] >= [StartDate]");
                    table.CheckConstraint("CK_Trips_Headcount", "[Headcount] >= 1");
                    table.ForeignKey(
                        name: "FK_Trips_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "TripSafety_HazardReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReporterUserId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ReportedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PhotoCapturedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    VerifiedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ResolvedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ExpiredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    VerifiedByUserId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ResolvedByUserId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ResolutionReason = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TripSafety_HazardReports", x => x.Id);
                    table.CheckConstraint("CK_TripSafety_HazardReports_ExpiredStatus", "[Status] <> 4 OR [ExpiredAtUtc] IS NOT NULL");
                    table.CheckConstraint("CK_TripSafety_HazardReports_Expiry", "[ExpiresAtUtc] = DATEADD(hour, 24, [ReportedAtUtc])");
                    table.CheckConstraint("CK_TripSafety_HazardReports_ExpiryTimestamp", "[ExpiredAtUtc] IS NULL OR [ExpiredAtUtc] >= [ExpiresAtUtc]");
                    table.CheckConstraint("CK_TripSafety_HazardReports_Resolution", "([ResolvedAtUtc] IS NULL AND [ResolvedByUserId] IS NULL AND [ResolutionReason] IS NULL) OR ([ResolvedAtUtc] IS NOT NULL AND [ResolvedByUserId] IS NOT NULL AND [ResolutionReason] IS NOT NULL AND [ResolvedAtUtc] >= [ReportedAtUtc])");
                    table.CheckConstraint("CK_TripSafety_HazardReports_ResolvedStatus", "[Status] <> 3 OR ([ResolvedAtUtc] IS NOT NULL AND [ResolvedByUserId] IS NOT NULL AND [ResolutionReason] IS NOT NULL)");
                    table.CheckConstraint("CK_TripSafety_HazardReports_Status", "[Status] IN (1, 2, 3, 4)");
                    table.CheckConstraint("CK_TripSafety_HazardReports_Verification", "([VerifiedAtUtc] IS NULL AND [VerifiedByUserId] IS NULL) OR ([VerifiedAtUtc] IS NOT NULL AND [VerifiedByUserId] IS NOT NULL AND [VerifiedAtUtc] >= [ReportedAtUtc])");
                    table.CheckConstraint("CK_TripSafety_HazardReports_VerifiedStatus", "[Status] <> 2 OR ([VerifiedAtUtc] IS NOT NULL AND [VerifiedByUserId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_TripSafety_HazardReports_Users_ReporterUserId",
                        column: x => x.ReporterUserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                    table.ForeignKey(
                        name: "FK_TripSafety_HazardReports_Users_ResolvedByUserId",
                        column: x => x.ResolvedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                    table.ForeignKey(
                        name: "FK_TripSafety_HazardReports_Users_VerifiedByUserId",
                        column: x => x.VerifiedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "UserProfiles",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    AvatarUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    Bio = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserProfiles", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_UserProfiles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "CoinRewardConsumerDeliveries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConsumerName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    DeliveredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastErrorCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoinRewardConsumerDeliveries", x => x.Id);
                    table.CheckConstraint("CK_CoinRewardConsumerDeliveries_Attempts", "[AttemptCount] >= 0");
                    table.CheckConstraint("CK_CoinRewardConsumerDeliveries_Status", "[Status] IN (1, 2, 3)");
                    table.ForeignKey(
                        name: "FK_CoinRewardConsumerDeliveries_CoinRewardOutbox_EventId",
                        column: x => x.EventId,
                        principalTable: "CoinRewardOutbox",
                        principalColumn: "EventId");
                });

            migrationBuilder.CreateTable(
                name: "CoinLedgerEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WalletId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeltaCoins = table.Column<long>(type: "bigint", nullable: false),
                    RewardType = table.Column<int>(type: "int", nullable: true),
                    SourceRecordId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    RuleVersion = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecordedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CorrectionOfEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoinLedgerEntries", x => x.Id);
                    table.CheckConstraint("CK_CoinLedgerEntries_NonZero", "[DeltaCoins] <> 0");
                    table.ForeignKey(
                        name: "FK_CoinLedgerEntries_CoinWallets_WalletId",
                        column: x => x.WalletId,
                        principalTable: "CoinWallets",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "SampleVoucherRedemptionRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WalletId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OfferId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerUserId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CostCoins = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AdminDecisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RequestedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DecidedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SampleVoucherRedemptionRequests", x => x.Id);
                    table.UniqueConstraint("AK_VoucherRequests_Id_Wallet", x => new { x.Id, x.WalletId });
                    table.CheckConstraint("CK_VoucherRequests_Amount", "[CostCoins] > 0");
                    table.CheckConstraint("CK_VoucherRequests_Status", "[Status] IN (1, 2, 3, 4)");
                    table.ForeignKey(
                        name: "FK_SampleVoucherRedemptionRequests_CoinWallets_WalletId_OwnerUserId",
                        columns: x => new { x.WalletId, x.OwnerUserId },
                        principalTable: "CoinWallets",
                        principalColumns: new[] { "Id", "UserId" });
                    table.ForeignKey(
                        name: "FK_SampleVoucherRedemptionRequests_SampleVoucherOffers_OfferId",
                        column: x => x.OfferId,
                        principalTable: "SampleVoucherOffers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SampleVoucherRedemptionRequests_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "GameAttempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GameId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlayerUserId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DeadlineUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameAttempts", x => x.Id);
                    table.UniqueConstraint("AK_GameAttempts_Id_Game", x => new { x.Id, x.GameId });
                    table.UniqueConstraint("AK_GameAttempts_Id_Game_Player", x => new { x.Id, x.GameId, x.PlayerUserId });
                    table.CheckConstraint("CK_GameAttempts_Deadline", "[DeadlineUtc] IS NULL OR [DeadlineUtc] > [StartedAtUtc]");
                    table.ForeignKey(
                        name: "FK_GameAttempts_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_GameAttempts_Users_PlayerUserId",
                        column: x => x.PlayerUserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "GameCreationApprovals",
                columns: table => new
                {
                    ApprovalRecordId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    GameId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AdminUserId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ApprovedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RewardEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RewardRuleVersion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameCreationApprovals", x => x.ApprovalRecordId);
                    table.ForeignKey(
                        name: "FK_GameCreationApprovals_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_GameCreationApprovals_Users_AdminUserId",
                        column: x => x.AdminUserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "GameModerationDecisions",
                columns: table => new
                {
                    DecisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GameId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AdminUserId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Action = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ResultingStatus = table.Column<int>(type: "int", nullable: false),
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameModerationDecisions", x => x.DecisionId);
                    table.CheckConstraint("CK_GameModeration_Action", "[Action] IN (1,2,3)");
                    table.CheckConstraint("CK_GameModeration_Status", "[ResultingStatus] IN (0,1,2,3,4,5)");
                    table.ForeignKey(
                        name: "FK_GameModerationDecisions_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_GameModerationDecisions_Users_AdminUserId",
                        column: x => x.AdminUserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "GameSteps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GameId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StepNumber = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameSteps", x => x.Id);
                    table.UniqueConstraint("AK_GameSteps_Game_StepNumber", x => new { x.GameId, x.StepNumber });
                    table.CheckConstraint("CK_GameSteps_Number", "[StepNumber] >= 1");
                    table.ForeignKey(
                        name: "FK_GameSteps_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Hunter_CaptureSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HunterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HunterSubmissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerUserId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false, computedColumnSql: "LOWER(CONVERT(nvarchar(256), [HunterId]))", stored: true),
                    IssuedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IsConsumed = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    IsRevoked = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Hunter_CaptureSessions", x => x.Id);
                    table.CheckConstraint("CK_Hunter_CaptureSessions_TimeWindow", "[ExpiresAtUtc] > [IssuedAtUtc]");
                    table.ForeignKey(
                        name: "FK_Hunter_CaptureSessions_Submission_Owner",
                        columns: x => new { x.HunterSubmissionId, x.OwnerUserId },
                        principalTable: "Hunter_Submissions",
                        principalColumns: new[] { "Id", "UserId" });
                });

            migrationBuilder.CreateTable(
                name: "Hunter_Evidence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HunterSubmissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EvidenceType = table.Column<int>(type: "int", nullable: false),
                    PrivateMediaReference = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    CapturedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UploadedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CaptureLatitude = table.Column<double>(type: "float", nullable: true),
                    CaptureLongitude = table.Column<double>(type: "float", nullable: true),
                    DurationSeconds = table.Column<double>(type: "float", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Hunter_Evidence", x => x.Id);
                    table.CheckConstraint("CK_Hunter_Evidence_CoordinatePair", "([CaptureLatitude] IS NULL AND [CaptureLongitude] IS NULL) OR ([CaptureLatitude] IS NOT NULL AND [CaptureLongitude] IS NOT NULL)");
                    table.CheckConstraint("CK_Hunter_Evidence_Duration", "[DurationSeconds] IS NULL OR [DurationSeconds] > 0");
                    table.CheckConstraint("CK_Hunter_Evidence_Type", "[EvidenceType] IN (1, 2)");
                    table.ForeignKey(
                        name: "FK_Hunter_Evidence_Submissions",
                        column: x => x.HunterSubmissionId,
                        principalTable: "Hunter_Submissions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Hunter_ReviewDecisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HunterSubmissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReviewerUserId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ResultingStatus = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ReviewedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Hunter_ReviewDecisions", x => x.Id);
                    table.CheckConstraint("CK_Hunter_ReviewDecisions_Status", "[ResultingStatus] IN (2, 3, 4)");
                    table.ForeignKey(
                        name: "FK_Hunter_ReviewDecisions_Submissions",
                        column: x => x.HunterSubmissionId,
                        principalTable: "Hunter_Submissions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TripSafety_LiveTripSessions",
                columns: table => new
                {
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TripPlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    FinishedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TripSafety_LiveTripSessions", x => x.SessionId);
                    table.CheckConstraint("CK_TripSafety_LiveTripSessions_Status", "[Status] IN (1, 2, 3)");
                    table.ForeignKey(
                        name: "FK_TripSafety_LiveTripSessions_Trips_TripPlanId",
                        column: x => x.TripPlanId,
                        principalTable: "Trips",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TripSafety_TripInvitations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TripPlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SenderUserId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    RecipientUserId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RespondedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TripSafety_TripInvitations", x => x.Id);
                    table.CheckConstraint("CK_TripSafety_TripInvitations_ResponseTime", "([Status] = 0 AND [RespondedAtUtc] IS NULL) OR ([Status] IN (1, 2, 3) AND [RespondedAtUtc] IS NOT NULL AND [RespondedAtUtc] >= [CreatedAtUtc])");
                    table.CheckConstraint("CK_TripSafety_TripInvitations_Status", "[Status] IN (0, 1, 2, 3)");
                    table.ForeignKey(
                        name: "FK_TripSafety_TripInvitations_Trips_TripPlanId",
                        column: x => x.TripPlanId,
                        principalTable: "Trips",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TripSafety_TripInvitations_Users_RecipientUserId",
                        column: x => x.RecipientUserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                    table.ForeignKey(
                        name: "FK_TripSafety_TripInvitations_Users_SenderUserId",
                        column: x => x.SenderUserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "TripStops",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TripPlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderIndex = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PlaceId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Latitude = table.Column<double>(type: "float", nullable: true),
                    Longitude = table.Column<double>(type: "float", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TripStops", x => x.Id);
                    table.CheckConstraint("CK_TripStops_Coordinates", "([Latitude] IS NULL AND [Longitude] IS NULL) OR ([Latitude] IS NOT NULL AND [Longitude] IS NOT NULL AND [Latitude] BETWEEN -90 AND 90 AND [Longitude] BETWEEN -180 AND 180)");
                    table.CheckConstraint("CK_TripStops_OrderIndex", "[OrderIndex] >= 0");
                    table.ForeignKey(
                        name: "FK_TripStops_Trips_TripPlanId",
                        column: x => x.TripPlanId,
                        principalTable: "Trips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CoinReservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WalletId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReservedCoins = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ResolvedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoinReservations", x => x.Id);
                    table.CheckConstraint("CK_CoinReservations_Amount", "[ReservedCoins] > 0");
                    table.CheckConstraint("CK_CoinReservations_Status", "[Status] IN (1, 2, 3)");
                    table.ForeignKey(
                        name: "FK_CoinReservations_CoinWallets_WalletId",
                        column: x => x.WalletId,
                        principalTable: "CoinWallets",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CoinReservations_SampleVoucherRedemptionRequests_RequestId_WalletId",
                        columns: x => new { x.RequestId, x.WalletId },
                        principalTable: "SampleVoucherRedemptionRequests",
                        principalColumns: new[] { "Id", "WalletId" });
                });

            migrationBuilder.CreateTable(
                name: "SampleVoucherInventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OfferId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CodeHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    EncryptedCodeReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    AssignedRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SampleVoucherInventory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SampleVoucherInventory_SampleVoucherOffers_OfferId",
                        column: x => x.OfferId,
                        principalTable: "SampleVoucherOffers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SampleVoucherInventory_SampleVoucherRedemptionRequests_AssignedRequestId",
                        column: x => x.AssignedRequestId,
                        principalTable: "SampleVoucherRedemptionRequests",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "GameAttemptProgress",
                columns: table => new
                {
                    AttemptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GameId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlayerUserId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    TotalSteps = table.Column<int>(type: "int", nullable: false),
                    CurrentStepNumber = table.Column<int>(type: "int", nullable: false),
                    RejectedEvidenceCount = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameAttemptProgress", x => x.AttemptId);
                    table.CheckConstraint("CK_GameProgress_Rejections", "[RejectedEvidenceCount] BETWEEN 0 AND 3");
                    table.CheckConstraint("CK_GameProgress_Status", "[Status] IN (0,1,2,3,4)");
                    table.CheckConstraint("CK_GameProgress_Steps", "[TotalSteps] > 0 AND [CurrentStepNumber] >= 1 AND [CurrentStepNumber] <= [TotalSteps]");
                    table.ForeignKey(
                        name: "FK_GameAttemptProgress_GameAttempts_AttemptId_GameId_PlayerUserId",
                        columns: x => new { x.AttemptId, x.GameId, x.PlayerUserId },
                        principalTable: "GameAttempts",
                        principalColumns: new[] { "Id", "GameId", "PlayerUserId" });
                });

            migrationBuilder.CreateTable(
                name: "GameCompletionResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GameId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttemptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlayerUserId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    AttemptStartedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AttemptDeadlineUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameCompletionResults", x => x.Id);
                    table.UniqueConstraint("AK_GameCompletionResults_Id_AttemptId", x => new { x.Id, x.AttemptId });
                    table.CheckConstraint("CK_GameCompletionResults_Status", "[Status] IN (0,1,2)");
                    table.CheckConstraint("CK_GameCompletionResults_Timeline", "[AttemptDeadlineUtc] > [AttemptStartedAtUtc] AND [CompletedAtUtc] >= [AttemptStartedAtUtc] AND [CompletedAtUtc] < [AttemptDeadlineUtc]");
                    table.ForeignKey(
                        name: "FK_GameCompletionResults_GameAttempts_AttemptId_GameId_PlayerUserId",
                        columns: x => new { x.AttemptId, x.GameId, x.PlayerUserId },
                        principalTable: "GameAttempts",
                        principalColumns: new[] { "Id", "GameId", "PlayerUserId" });
                    table.ForeignKey(
                        name: "FK_GameCompletionResults_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_GameCompletionResults_Users_PlayerUserId",
                        column: x => x.PlayerUserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "GameResultDecisions",
                columns: table => new
                {
                    DecisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GameId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttemptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AdminUserId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    IsApproved = table.Column<bool>(type: "bit", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameResultDecisions", x => x.DecisionId);
                    table.ForeignKey(
                        name: "FK_GameResultDecisions_GameAttempts_AttemptId_GameId",
                        columns: x => new { x.AttemptId, x.GameId },
                        principalTable: "GameAttempts",
                        principalColumns: new[] { "Id", "GameId" });
                    table.ForeignKey(
                        name: "FK_GameResultDecisions_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_GameResultDecisions_Users_AdminUserId",
                        column: x => x.AdminUserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "GameWinners",
                columns: table => new
                {
                    GameId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttemptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlayerUserId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ApprovedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ApprovedByUserId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameWinners", x => x.GameId);
                    table.ForeignKey(
                        name: "FK_GameWinners_GameAttempts_AttemptId_GameId_PlayerUserId",
                        columns: x => new { x.AttemptId, x.GameId, x.PlayerUserId },
                        principalTable: "GameAttempts",
                        principalColumns: new[] { "Id", "GameId", "PlayerUserId" });
                    table.ForeignKey(
                        name: "FK_GameWinners_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_GameWinners_Users_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                    table.ForeignKey(
                        name: "FK_GameWinners_Users_PlayerUserId",
                        column: x => x.PlayerUserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "GameHints",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GameStepId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TextHint = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    PhotoMediaId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameHints", x => x.Id);
                    table.CheckConstraint("CK_GameHints_Content", "[TextHint] IS NOT NULL OR [PhotoMediaId] IS NOT NULL");
                    table.ForeignKey(
                        name: "FK_GameHints_GameSteps_GameStepId",
                        column: x => x.GameStepId,
                        principalTable: "GameSteps",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "GameProofEvidence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttemptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GameId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StepNumber = table.Column<int>(type: "int", nullable: false),
                    MediaId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    SubmittedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameProofEvidence", x => x.Id);
                    table.UniqueConstraint("AK_GameProofEvidence_Id_Attempt", x => new { x.Id, x.AttemptId });
                    table.CheckConstraint("CK_GameProofEvidence_Step", "[StepNumber] >= 1");
                    table.ForeignKey(
                        name: "FK_GameProofEvidence_GameAttempts_AttemptId_GameId",
                        columns: x => new { x.AttemptId, x.GameId },
                        principalTable: "GameAttempts",
                        principalColumns: new[] { "Id", "GameId" });
                    table.ForeignKey(
                        name: "FK_GameProofEvidence_GameSteps_GameId_StepNumber",
                        columns: x => new { x.GameId, x.StepNumber },
                        principalTable: "GameSteps",
                        principalColumns: new[] { "GameId", "StepNumber" });
                });

            migrationBuilder.CreateTable(
                name: "TripSafety_GpsConsents",
                columns: table => new
                {
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    IsGranted = table.Column<bool>(type: "bit", nullable: false),
                    GrantedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RevokedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TripSafety_GpsConsents", x => new { x.SessionId, x.UserId });
                    table.CheckConstraint("CK_TripSafety_GpsConsents_GrantState", "([IsGranted] = 0) OR ([IsGranted] = 1 AND [GrantedAtUtc] IS NOT NULL AND [RevokedAtUtc] IS NULL)");
                    table.CheckConstraint("CK_TripSafety_GpsConsents_RevocationTime", "[RevokedAtUtc] IS NULL OR ([GrantedAtUtc] IS NOT NULL AND [RevokedAtUtc] >= [GrantedAtUtc])");
                    table.ForeignKey(
                        name: "FK_TripSafety_GpsConsents_LiveTripSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "TripSafety_LiveTripSessions",
                        principalColumn: "SessionId");
                    table.ForeignKey(
                        name: "FK_TripSafety_GpsConsents_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "TripSafety_TripMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TripPlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MemberUserId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    InvitationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JoinedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RemovedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TripSafety_TripMembers", x => x.Id);
                    table.CheckConstraint("CK_TripSafety_TripMembers_RemovalState", "([IsActive] = 1 AND [RemovedAtUtc] IS NULL) OR ([IsActive] = 0 AND [RemovedAtUtc] IS NOT NULL AND [RemovedAtUtc] >= [JoinedAtUtc])");
                    table.ForeignKey(
                        name: "FK_TripSafety_TripMembers_TripInvitations_InvitationId",
                        column: x => x.InvitationId,
                        principalTable: "TripSafety_TripInvitations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TripSafety_TripMembers_Trips_TripPlanId",
                        column: x => x.TripPlanId,
                        principalTable: "Trips",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TripSafety_TripMembers_Users_MemberUserId",
                        column: x => x.MemberUserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "SampleVoucherIssuances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    QrPayload = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    ImageReference = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    IssuedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SampleVoucherIssuances", x => x.Id);
                    table.CheckConstraint("CK_VoucherIssuances_Status", "[Status] IN (1, 2, 3)");
                    table.ForeignKey(
                        name: "FK_SampleVoucherIssuances_SampleVoucherInventory_InventoryId",
                        column: x => x.InventoryId,
                        principalTable: "SampleVoucherInventory",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SampleVoucherIssuances_SampleVoucherRedemptionRequests_RequestId",
                        column: x => x.RequestId,
                        principalTable: "SampleVoucherRedemptionRequests",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "GameCompletionEvidence",
                columns: table => new
                {
                    CompletionResultId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EvidenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttemptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameCompletionEvidence", x => new { x.CompletionResultId, x.EvidenceId });
                    table.ForeignKey(
                        name: "FK_GameCompletionEvidence_GameCompletionResults_CompletionResultId_AttemptId",
                        columns: x => new { x.CompletionResultId, x.AttemptId },
                        principalTable: "GameCompletionResults",
                        principalColumns: new[] { "Id", "AttemptId" });
                    table.ForeignKey(
                        name: "FK_GameCompletionEvidence_GameProofEvidence_EvidenceId_AttemptId",
                        columns: x => new { x.EvidenceId, x.AttemptId },
                        principalTable: "GameProofEvidence",
                        principalColumns: new[] { "Id", "AttemptId" });
                });

            migrationBuilder.CreateTable(
                name: "GameProofReviewDecisions",
                columns: table => new
                {
                    DecisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EvidenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReviewerUserId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    IsApproved = table.Column<bool>(type: "bit", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ReviewedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameProofReviewDecisions", x => x.DecisionId);
                    table.ForeignKey(
                        name: "FK_GameProofReviewDecisions_GameProofEvidence_EvidenceId",
                        column: x => x.EvidenceId,
                        principalTable: "GameProofEvidence",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_GameProofReviewDecisions_Users_ReviewerUserId",
                        column: x => x.ReviewerUserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Advertisements_Enabled_Window",
                table: "Advertisements",
                columns: new[] { "IsEnabled", "StartAtUtc", "EndAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CoinLedgerEntries_WalletId",
                table: "CoinLedgerEntries",
                column: "WalletId");

            migrationBuilder.CreateIndex(
                name: "UX_CoinLedgerEntries_GameWinnerSource",
                table: "CoinLedgerEntries",
                columns: new[] { "RewardType", "SourceRecordId" },
                unique: true,
                filter: "[RewardType] = 4");

            migrationBuilder.CreateIndex(
                name: "UX_CoinLedgerEntries_Wallet_RewardSource",
                table: "CoinLedgerEntries",
                columns: new[] { "WalletId", "RewardType", "SourceRecordId" },
                unique: true,
                filter: "[RewardType] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CoinReservations_RequestId_WalletId",
                table: "CoinReservations",
                columns: new[] { "RequestId", "WalletId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CoinReservations_WalletId",
                table: "CoinReservations",
                column: "WalletId");

            migrationBuilder.CreateIndex(
                name: "UX_CoinReservations_RequestId",
                table: "CoinReservations",
                column: "RequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_CoinConsumer_Event_Name",
                table: "CoinRewardConsumerDeliveries",
                columns: new[] { "EventId", "ConsumerName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CoinOutbox_Due",
                table: "CoinRewardOutbox",
                columns: new[] { "Status", "NextDueAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CoinRewardOutbox_RecipientUserId",
                table: "CoinRewardOutbox",
                column: "RecipientUserId");

            migrationBuilder.CreateIndex(
                name: "UX_CoinOutbox_GameWinnerSource",
                table: "CoinRewardOutbox",
                columns: new[] { "RewardType", "SourceRecordId" },
                unique: true,
                filter: "[RewardType] = 4");

            migrationBuilder.CreateIndex(
                name: "UX_CoinOutbox_Recipient_RewardSource",
                table: "CoinRewardOutbox",
                columns: new[] { "RewardType", "SourceRecordId", "RecipientUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_CoinRuleVersions_VersionCode",
                table: "CoinRuleVersions",
                column: "VersionCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_CoinWallets_UserId",
                table: "CoinWallets",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GameAttemptProgress_AttemptId_GameId_PlayerUserId",
                table: "GameAttemptProgress",
                columns: new[] { "AttemptId", "GameId", "PlayerUserId" });

            migrationBuilder.CreateIndex(
                name: "UX_GameProgress_Active_Game_Player",
                table: "GameAttemptProgress",
                columns: new[] { "GameId", "PlayerUserId" },
                unique: true,
                filter: "[Status] IN (0,1,2)");

            migrationBuilder.CreateIndex(
                name: "IX_GameAttempts_Game_Player",
                table: "GameAttempts",
                columns: new[] { "GameId", "PlayerUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_GameAttempts_PlayerUserId",
                table: "GameAttempts",
                column: "PlayerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_GameCompletionEvidence_CompletionResultId_AttemptId",
                table: "GameCompletionEvidence",
                columns: new[] { "CompletionResultId", "AttemptId" });

            migrationBuilder.CreateIndex(
                name: "IX_GameCompletionEvidence_EvidenceId_AttemptId",
                table: "GameCompletionEvidence",
                columns: new[] { "EvidenceId", "AttemptId" });

            migrationBuilder.CreateIndex(
                name: "UX_GameCompletionEvidence_Evidence",
                table: "GameCompletionEvidence",
                column: "EvidenceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GameCompletionResults_AttemptId_GameId_PlayerUserId",
                table: "GameCompletionResults",
                columns: new[] { "AttemptId", "GameId", "PlayerUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_GameCompletionResults_Game_Status_Time",
                table: "GameCompletionResults",
                columns: new[] { "GameId", "Status", "CompletedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_GameCompletionResults_PlayerUserId",
                table: "GameCompletionResults",
                column: "PlayerUserId");

            migrationBuilder.CreateIndex(
                name: "UX_GameCompletionResults_Attempt",
                table: "GameCompletionResults",
                column: "AttemptId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GameCreationApprovals_AdminUserId",
                table: "GameCreationApprovals",
                column: "AdminUserId");

            migrationBuilder.CreateIndex(
                name: "UX_GameCreationApprovals_Correlation",
                table: "GameCreationApprovals",
                column: "CorrelationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_GameCreationApprovals_Game",
                table: "GameCreationApprovals",
                column: "GameId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_GameCreationApprovals_RewardEvent",
                table: "GameCreationApprovals",
                column: "RewardEventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GameHints_GameStepId",
                table: "GameHints",
                column: "GameStepId");

            migrationBuilder.CreateIndex(
                name: "IX_GameModeration_Game_Time",
                table: "GameModerationDecisions",
                columns: new[] { "GameId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_GameModerationDecisions_AdminUserId",
                table: "GameModerationDecisions",
                column: "AdminUserId");

            migrationBuilder.CreateIndex(
                name: "UX_GameModeration_Correlation",
                table: "GameModerationDecisions",
                column: "CorrelationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GameProofEvidence_Attempt_Step_Time",
                table: "GameProofEvidence",
                columns: new[] { "AttemptId", "StepNumber", "SubmittedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_GameProofEvidence_AttemptId_GameId",
                table: "GameProofEvidence",
                columns: new[] { "AttemptId", "GameId" });

            migrationBuilder.CreateIndex(
                name: "IX_GameProofEvidence_GameId_StepNumber",
                table: "GameProofEvidence",
                columns: new[] { "GameId", "StepNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_GameProofReviewDecisions_ReviewerUserId",
                table: "GameProofReviewDecisions",
                column: "ReviewerUserId");

            migrationBuilder.CreateIndex(
                name: "UX_GameProofReviews_Correlation",
                table: "GameProofReviewDecisions",
                column: "CorrelationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_GameProofReviews_Evidence",
                table: "GameProofReviewDecisions",
                column: "EvidenceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GameResultDecisions_AdminUserId",
                table: "GameResultDecisions",
                column: "AdminUserId");

            migrationBuilder.CreateIndex(
                name: "IX_GameResultDecisions_AttemptId_GameId",
                table: "GameResultDecisions",
                columns: new[] { "AttemptId", "GameId" });

            migrationBuilder.CreateIndex(
                name: "IX_GameResultDecisions_Game_Time",
                table: "GameResultDecisions",
                columns: new[] { "GameId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_GameResults_Correlation",
                table: "GameResultDecisions",
                column: "CorrelationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_GameResults_OneApprovedPerGame",
                table: "GameResultDecisions",
                column: "GameId",
                unique: true,
                filter: "[IsApproved] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Games_Creator_Submitted",
                table: "Games",
                columns: new[] { "CreatorUserId", "SubmittedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Games_Status_Expiry",
                table: "Games",
                columns: new[] { "Status", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_GameSteps_Game_Number",
                table: "GameSteps",
                columns: new[] { "GameId", "StepNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GameWinners_ApprovedByUserId",
                table: "GameWinners",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_GameWinners_AttemptId_GameId_PlayerUserId",
                table: "GameWinners",
                columns: new[] { "AttemptId", "GameId", "PlayerUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_GameWinners_PlayerUserId",
                table: "GameWinners",
                column: "PlayerUserId");

            migrationBuilder.CreateIndex(
                name: "UX_GameWinners_Attempt",
                table: "GameWinners",
                column: "AttemptId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Hunter_CaptureSessions_Hunter_State",
                table: "Hunter_CaptureSessions",
                columns: new[] { "HunterId", "IsRevoked", "IsConsumed" });

            migrationBuilder.CreateIndex(
                name: "IX_Hunter_CaptureSessions_HunterSubmissionId_OwnerUserId",
                table: "Hunter_CaptureSessions",
                columns: new[] { "HunterSubmissionId", "OwnerUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_Hunter_CaptureSessions_Submission_State",
                table: "Hunter_CaptureSessions",
                columns: new[] { "HunterSubmissionId", "IsRevoked", "IsConsumed" });

            migrationBuilder.CreateIndex(
                name: "IX_Hunter_Evidence_Submission_Type",
                table: "Hunter_Evidence",
                columns: new[] { "HunterSubmissionId", "EvidenceType" });

            migrationBuilder.CreateIndex(
                name: "IX_Hunter_ReviewDecisions_Submission_Date",
                table: "Hunter_ReviewDecisions",
                columns: new[] { "HunterSubmissionId", "ReviewedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Hunter_Submissions_District_Status",
                table: "Hunter_Submissions",
                columns: new[] { "District", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Hunter_Submissions_UserId",
                table: "Hunter_Submissions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_IdentityConsents_UserId_ConsentKey",
                table: "IdentityConsents",
                columns: new[] { "UserId", "ConsentKey" });

            migrationBuilder.CreateIndex(
                name: "IX_IdentityOtpChallenges_ExpiresAtUtc",
                table: "IdentityOtpChallenges",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_IdentityOtpChallenges_UserId",
                table: "IdentityOtpChallenges",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_IdentitySessions_ExpiresAtUtc",
                table: "IdentitySessions",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_IdentitySessions_UserId",
                table: "IdentitySessions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_SampleVoucherInventory_OfferId",
                table: "SampleVoucherInventory",
                column: "OfferId");

            migrationBuilder.CreateIndex(
                name: "UX_VoucherInventory_Assignment",
                table: "SampleVoucherInventory",
                column: "AssignedRequestId",
                unique: true,
                filter: "[AssignedRequestId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_VoucherInventory_CodeHash",
                table: "SampleVoucherInventory",
                column: "CodeHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_VoucherIssuances_Inventory",
                table: "SampleVoucherIssuances",
                column: "InventoryId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_VoucherIssuances_Request",
                table: "SampleVoucherIssuances",
                column: "RequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_VoucherOffers_Code",
                table: "SampleVoucherOffers",
                column: "OfferCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SampleVoucherRedemptionRequests_OfferId",
                table: "SampleVoucherRedemptionRequests",
                column: "OfferId");

            migrationBuilder.CreateIndex(
                name: "IX_SampleVoucherRedemptionRequests_OwnerUserId",
                table: "SampleVoucherRedemptionRequests",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SampleVoucherRedemptionRequests_WalletId_OwnerUserId",
                table: "SampleVoucherRedemptionRequests",
                columns: new[] { "WalletId", "OwnerUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_VoucherRequests_Wallet_Status",
                table: "SampleVoucherRedemptionRequests",
                columns: new[] { "WalletId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Trips_OwnerUserId",
                table: "Trips",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TripSafety_GpsConsents_UserId",
                table: "TripSafety_GpsConsents",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_TripSafety_HazardReports_Reporter_ReportedAtUtc",
                table: "TripSafety_HazardReports",
                columns: new[] { "ReporterUserId", "ReportedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TripSafety_HazardReports_ResolvedByUserId",
                table: "TripSafety_HazardReports",
                column: "ResolvedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TripSafety_HazardReports_Status_ExpiresAtUtc",
                table: "TripSafety_HazardReports",
                columns: new[] { "Status", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TripSafety_HazardReports_VerifiedByUserId",
                table: "TripSafety_HazardReports",
                column: "VerifiedByUserId");

            migrationBuilder.CreateIndex(
                name: "UX_TripSafety_LiveTripSessions_OpenPlan",
                table: "TripSafety_LiveTripSessions",
                column: "TripPlanId",
                unique: true,
                filter: "[Status] IN (1, 2)");

            migrationBuilder.CreateIndex(
                name: "IX_TripSafety_TripInvitations_Recipient_Status",
                table: "TripSafety_TripInvitations",
                columns: new[] { "RecipientUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_TripSafety_TripInvitations_Sender_Created",
                table: "TripSafety_TripInvitations",
                columns: new[] { "SenderUserId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_TripSafety_TripInvitations_OpenRecipient",
                table: "TripSafety_TripInvitations",
                columns: new[] { "TripPlanId", "RecipientUserId" },
                unique: true,
                filter: "[Status] IN (0, 1)");

            migrationBuilder.CreateIndex(
                name: "IX_TripSafety_TripMembers_MemberUserId",
                table: "TripSafety_TripMembers",
                column: "MemberUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TripSafety_TripMembers_Trip_Active",
                table: "TripSafety_TripMembers",
                columns: new[] { "TripPlanId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "UX_TripSafety_TripMembers_ActiveTripUser",
                table: "TripSafety_TripMembers",
                columns: new[] { "TripPlanId", "MemberUserId" },
                unique: true,
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "UX_TripSafety_TripMembers_InvitationId",
                table: "TripSafety_TripMembers",
                column: "InvitationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TripStops_TripPlanId_OrderIndex",
                table: "TripStops",
                columns: new[] { "TripPlanId", "OrderIndex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Users_NormalizedEmail",
                table: "Users",
                column: "NormalizedEmail",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Advertisements");

            migrationBuilder.DropTable(
                name: "CoinLedgerEntries");

            migrationBuilder.DropTable(
                name: "CoinReservations");

            migrationBuilder.DropTable(
                name: "CoinRewardConsumerDeliveries");

            migrationBuilder.DropTable(
                name: "CoinRuleVersions");

            migrationBuilder.DropTable(
                name: "GameAttemptProgress");

            migrationBuilder.DropTable(
                name: "GameCompletionEvidence");

            migrationBuilder.DropTable(
                name: "GameCreationApprovals");

            migrationBuilder.DropTable(
                name: "GameHints");

            migrationBuilder.DropTable(
                name: "GameModerationDecisions");

            migrationBuilder.DropTable(
                name: "GameProofReviewDecisions");

            migrationBuilder.DropTable(
                name: "GameResultDecisions");

            migrationBuilder.DropTable(
                name: "GameWinners");

            migrationBuilder.DropTable(
                name: "Hunter_CaptureSessions");

            migrationBuilder.DropTable(
                name: "Hunter_Evidence");

            migrationBuilder.DropTable(
                name: "Hunter_ReviewDecisions");

            migrationBuilder.DropTable(
                name: "IdentityConsents");

            migrationBuilder.DropTable(
                name: "IdentityOtpChallenges");

            migrationBuilder.DropTable(
                name: "IdentitySessions");

            migrationBuilder.DropTable(
                name: "SampleVoucherIssuances");

            migrationBuilder.DropTable(
                name: "TripSafety_GpsConsents");

            migrationBuilder.DropTable(
                name: "TripSafety_HazardReports");

            migrationBuilder.DropTable(
                name: "TripSafety_TripMembers");

            migrationBuilder.DropTable(
                name: "TripStops");

            migrationBuilder.DropTable(
                name: "UserProfiles");

            migrationBuilder.DropTable(
                name: "CoinRewardOutbox");

            migrationBuilder.DropTable(
                name: "GameCompletionResults");

            migrationBuilder.DropTable(
                name: "GameProofEvidence");

            migrationBuilder.DropTable(
                name: "Hunter_Submissions");

            migrationBuilder.DropTable(
                name: "SampleVoucherInventory");

            migrationBuilder.DropTable(
                name: "TripSafety_LiveTripSessions");

            migrationBuilder.DropTable(
                name: "TripSafety_TripInvitations");

            migrationBuilder.DropTable(
                name: "GameAttempts");

            migrationBuilder.DropTable(
                name: "GameSteps");

            migrationBuilder.DropTable(
                name: "SampleVoucherRedemptionRequests");

            migrationBuilder.DropTable(
                name: "Trips");

            migrationBuilder.DropTable(
                name: "Games");

            migrationBuilder.DropTable(
                name: "CoinWallets");

            migrationBuilder.DropTable(
                name: "SampleVoucherOffers");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
