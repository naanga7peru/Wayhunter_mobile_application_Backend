using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayhunter.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReconcileSharedModulePersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_VoucherIssuances_Inventory",
                table: "SampleVoucherIssuances");

            migrationBuilder.DropCheckConstraint(
                name: "CK_VoucherIssuances_Status",
                table: "SampleVoucherIssuances");

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "TripSafety_HazardReports",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "TripSafety_HazardReports",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "TripSafety_HazardReports",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhotoAssetReference",
                table: "TripSafety_HazardReports",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DecisionActorUserId",
                table: "SampleVoucherRedemptionRequests",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DecisionReason",
                table: "SampleVoucherRedemptionRequests",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OwnerNameSnapshot",
                table: "SampleVoucherRedemptionRequests",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<Guid>(
                name: "InventoryId",
                table: "SampleVoucherIssuances",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<int>(
                name: "AttemptCount",
                table: "SampleVoucherIssuances",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ExpiresAtUtc",
                table: "SampleVoucherIssuances",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastErrorCode",
                table: "SampleVoucherIssuances",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "SampleVoucherIssuances",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<string>(
                name: "UseReason",
                table: "SampleVoucherIssuances",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UsedAtUtc",
                table: "SampleVoucherIssuances",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UsedByActorUserId",
                table: "SampleVoucherIssuances",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AddedAtUtc",
                table: "SampleVoucherInventory",
                type: "datetimeoffset",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "AddedByAdminUserId",
                table: "SampleVoucherInventory",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ExpiresAtUtc",
                table: "SampleVoucherInventory",
                type: "datetimeoffset",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "SampleVoucherInventory",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<string>(
                name: "SampleImageReference",
                table: "SampleVoucherInventory",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAtUtc",
                table: "CoinRuleVersions",
                type: "datetimeoffset",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "CreatedByUserId",
                table: "CoinRuleVersions",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EffectiveFromUtc",
                table: "CoinRuleVersions",
                type: "datetimeoffset",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EffectiveUntilUtc",
                table: "CoinRuleVersions",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ActorUserId",
                table: "CoinLedgerEntries",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Reason",
                table: "CoinLedgerEntries",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_TripStops_Id_TripPlanId",
                table: "TripStops",
                columns: new[] { "Id", "TripPlanId" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_TripSafety_LiveTripSessions_SessionId_TripPlanId",
                table: "TripSafety_LiveTripSessions",
                columns: new[] { "SessionId", "TripPlanId" });

            migrationBuilder.CreateTable(
                name: "TripPlanCopyAttributions",
                columns: table => new
                {
                    TripPlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceTripPlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceOwnerUserId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CopiedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TripPlanCopyAttributions", x => x.TripPlanId);
                    table.CheckConstraint("CK_TripPlanCopies_NotOwnSource", "[TripPlanId] <> [SourceTripPlanId]");
                    table.ForeignKey(
                        name: "FK_TripPlanCopyAttributions_Trips_TripPlanId",
                        column: x => x.TripPlanId,
                        principalTable: "Trips",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TripSafety_MemberRevocationOutbox",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TripPlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MemberUserId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    RemovedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IsDelivered = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeliveredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeliveryAttempts = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    NextAttemptAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LeaseToken = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LeaseUntilUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeadLetteredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TripSafety_MemberRevocationOutbox", x => x.Id);
                    table.CheckConstraint("CK_TripSafety_MemberRevocationOutbox_Attempts", "[DeliveryAttempts] >= 0");
                    table.CheckConstraint("CK_TripSafety_MemberRevocationOutbox_DeliveryState", "([IsDelivered] = 0 AND [DeliveredAtUtc] IS NULL) OR ([IsDelivered] = 1 AND [DeliveredAtUtc] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_TripSafety_MemberRevocationOutbox_TripSafety_TripMembers_MemberId",
                        column: x => x.MemberId,
                        principalTable: "TripSafety_TripMembers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TripSafety_NotificationAudienceReceipts",
                columns: table => new
                {
                    ReceiptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Fingerprint = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    Count = table.Column<int>(type: "int", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ConsumedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TripSafety_NotificationAudienceReceipts", x => x.ReceiptId);
                    table.CheckConstraint("CK_MS08_NotificationAudience_Count", "[Count] BETWEEN 1 AND 100");
                    table.ForeignKey(
                        name: "FK_TripSafety_NotificationAudienceReceipts_Users_ActorId",
                        column: x => x.ActorId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "TripSafety_NotificationDevices",
                columns: table => new
                {
                    DeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    TokenFingerprint = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    ProtectedToken = table.Column<byte[]>(type: "varbinary(max)", maxLength: 8192, nullable: false),
                    PushAllowed = table.Column<bool>(type: "bit", nullable: false),
                    ChangedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RevokedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TripSafety_NotificationDevices", x => x.DeviceId);
                    table.ForeignKey(
                        name: "FK_TripSafety_NotificationDevices_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "TripSafety_NotificationJobs",
                columns: table => new
                {
                    JobId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    State = table.Column<int>(type: "int", nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    DueAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Template = table.Column<int>(type: "int", nullable: false),
                    TemplateVersion = table.Column<int>(type: "int", nullable: false),
                    TargetId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TripPlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PlannedStartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    TripVersion = table.Column<string>(type: "varchar(16)", unicode: false, maxLength: 16, nullable: true),
                    AudienceRole = table.Column<int>(type: "int", nullable: true),
                    AudienceDistrict = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    AudienceFingerprint = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TripSafety_NotificationJobs", x => x.JobId);
                    table.CheckConstraint("CK_MS08_NotificationJob_Kind", "([Kind] = 1 AND [TripPlanId] IS NOT NULL AND [SessionId] IS NOT NULL AND [TripVersion] IS NOT NULL AND [PlannedStartDate] IS NOT NULL) OR ([Kind] = 2 AND [TripPlanId] IS NULL AND [SessionId] IS NULL AND [TripVersion] IS NULL AND [PlannedStartDate] IS NULL AND [AudienceFingerprint] IS NOT NULL)");
                    table.CheckConstraint("CK_MS08_NotificationJob_State", "[State] BETWEEN 0 AND 2 AND [Revision] > 0");
                    table.ForeignKey(
                        name: "FK_TripSafety_NotificationJobs_TripSafety_LiveTripSessions_SessionId_TripPlanId",
                        columns: x => new { x.SessionId, x.TripPlanId },
                        principalTable: "TripSafety_LiveTripSessions",
                        principalColumns: new[] { "SessionId", "TripPlanId" });
                    table.ForeignKey(
                        name: "FK_TripSafety_NotificationJobs_Trips_TripPlanId",
                        column: x => x.TripPlanId,
                        principalTable: "Trips",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TripSafety_NotificationJobs_Users_ActorId",
                        column: x => x.ActorId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "TripSafety_NotificationOperations",
                columns: table => new
                {
                    ActorId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    OperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    Fingerprint = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    JobState = table.Column<int>(type: "int", nullable: true),
                    Revision = table.Column<int>(type: "int", nullable: true),
                    DueAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RecordedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TripSafety_NotificationOperations", x => new { x.ActorId, x.OperationId });
                    table.ForeignKey(
                        name: "FK_TripSafety_NotificationOperations_Users_ActorId",
                        column: x => x.ActorId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "TripSafety_NotificationPreferences",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Category = table.Column<int>(type: "int", nullable: false),
                    PushEnabled = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TripSafety_NotificationPreferences", x => new { x.UserId, x.Category });
                    table.CheckConstraint("CK_MS08_NotificationPreference_Category", "[Category] BETWEEN 1 AND 7");
                    table.ForeignKey(
                        name: "FK_TripSafety_NotificationPreferences_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "TripSafety_SafeCheckIns",
                columns: table => new
                {
                    CheckInId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TripPlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CheckedInAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TripSafety_SafeCheckIns", x => x.CheckInId);
                    table.ForeignKey(
                        name: "FK_TripSafety_SafeCheckIns_LiveTripSessions_SessionId_TripPlanId",
                        columns: x => new { x.SessionId, x.TripPlanId },
                        principalTable: "TripSafety_LiveTripSessions",
                        principalColumns: new[] { "SessionId", "TripPlanId" });
                    table.ForeignKey(
                        name: "FK_TripSafety_SafeCheckIns_Trips_TripPlanId",
                        column: x => x.TripPlanId,
                        principalTable: "Trips",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TripSafety_SafeCheckIns_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "TripSafety_TripMemories",
                columns: table => new
                {
                    MemoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TripPlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TripStopId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OwnerUserId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreateOperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Caption = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Visibility = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TripSafety_TripMemories", x => x.MemoryId);
                    table.CheckConstraint("CK_TripSafety_TripMemories_Timestamps", "[UpdatedAtUtc] >= [CreatedAtUtc] AND ([DeletedAtUtc] IS NULL OR [DeletedAtUtc] >= [CreatedAtUtc])");
                    table.CheckConstraint("CK_TripSafety_TripMemories_Visibility", "[Visibility] IN (0, 1)");
                    table.ForeignKey(
                        name: "FK_TripSafety_TripMemories_TripStops_TripStopId_TripPlanId",
                        columns: x => new { x.TripStopId, x.TripPlanId },
                        principalTable: "TripStops",
                        principalColumns: new[] { "Id", "TripPlanId" });
                    table.ForeignKey(
                        name: "FK_TripSafety_TripMemories_Trips_TripPlanId",
                        column: x => x.TripPlanId,
                        principalTable: "Trips",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TripSafety_TripMemories_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "UserRoleAssignments",
                columns: table => new
                {
                    AssignmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Role = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    AssignedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AssignedByUserId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    RevokedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RevokedByUserId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRoleAssignments", x => x.AssignmentId);
                    table.CheckConstraint("CK_UserRoleAssignments_AssignmentTime", "[AssignedAtUtc] IS NOT NULL");
                    table.CheckConstraint("CK_UserRoleAssignments_Revocation", "(\r\n                    ([Status] = N'Active'\r\n                        AND [RevokedAtUtc] IS NULL\r\n                        AND [RevokedByUserId] IS NULL)\r\n                    OR\r\n                    ([Status] = N'Inactive'\r\n                        AND [RevokedAtUtc] IS NULL\r\n                        AND [RevokedByUserId] IS NULL)\r\n                    OR\r\n                    ([Status] = N'Revoked'\r\n                        AND [RevokedAtUtc] IS NOT NULL\r\n                        AND [RevokedAtUtc] >= [AssignedAtUtc])\r\n                )");
                    table.CheckConstraint("CK_UserRoleAssignments_Role", "[Role] = N'Admin'");
                    table.CheckConstraint("CK_UserRoleAssignments_Status", "[Status] IN (N'Active', N'Revoked', N'Inactive')");
                    table.ForeignKey(
                        name: "FK_UserRoleAssignments_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "TripSafety_NotificationTokenBindings",
                columns: table => new
                {
                    Fingerprint = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    DeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    RecordedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TripSafety_NotificationTokenBindings", x => x.Fingerprint);
                    table.ForeignKey(
                        name: "FK_TripSafety_NotificationTokenBindings_TripSafety_NotificationDevices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "TripSafety_NotificationDevices",
                        principalColumn: "DeviceId");
                    table.ForeignKey(
                        name: "FK_TripSafety_NotificationTokenBindings_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "TripSafety_NotificationEvents",
                columns: table => new
                {
                    EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Category = table.Column<int>(type: "int", nullable: false),
                    Template = table.Column<int>(type: "int", nullable: false),
                    TemplateVersion = table.Column<int>(type: "int", nullable: false),
                    TargetId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    JobId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TripSafety_NotificationEvents", x => x.EventId);
                    table.CheckConstraint("CK_MS08_NotificationEvent_Template", "[Category] BETWEEN 1 AND 7 AND [Template] BETWEEN 1 AND 7 AND [TemplateVersion] > 0");
                    table.ForeignKey(
                        name: "FK_TripSafety_NotificationEvents_TripSafety_NotificationJobs_JobId",
                        column: x => x.JobId,
                        principalTable: "TripSafety_NotificationJobs",
                        principalColumn: "JobId");
                });

            migrationBuilder.CreateTable(
                name: "TripSafety_NotificationJobRecipients",
                columns: table => new
                {
                    JobId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TripSafety_NotificationJobRecipients", x => new { x.JobId, x.UserId });
                    table.ForeignKey(
                        name: "FK_TripSafety_NotificationJobRecipients_TripSafety_NotificationJobs_JobId",
                        column: x => x.JobId,
                        principalTable: "TripSafety_NotificationJobs",
                        principalColumn: "JobId");
                    table.ForeignKey(
                        name: "FK_TripSafety_NotificationJobRecipients_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "TripSafety_SafeCheckInOutbox",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CheckInId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecipientUserId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                    DeliveryAttempts = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    IsDelivered = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeliveredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: true),
                    NextAttemptAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: true),
                    LeaseToken = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LeaseUntilUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: true),
                    DeadLetteredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TripSafety_SafeCheckInOutbox", x => x.Id);
                    table.CheckConstraint("CK_TripSafety_SafeCheckInOutbox_Attempts", "[DeliveryAttempts] >= 0");
                    table.CheckConstraint("CK_TripSafety_SafeCheckInOutbox_DeliveryState", "([IsDelivered] = 0 AND [DeliveredAtUtc] IS NULL) OR ([IsDelivered] = 1 AND [DeliveredAtUtc] IS NOT NULL AND [DeadLetteredAtUtc] IS NULL)");
                    table.CheckConstraint("CK_TripSafety_SafeCheckInOutbox_LeasePair", "([LeaseToken] IS NULL AND [LeaseUntilUtc] IS NULL) OR ([LeaseToken] IS NOT NULL AND [LeaseUntilUtc] IS NOT NULL)");
                    table.CheckConstraint("CK_TripSafety_SafeCheckInOutbox_TerminalState", "([IsDelivered] = 0 AND [DeadLetteredAtUtc] IS NULL) OR ([LeaseToken] IS NULL AND [LeaseUntilUtc] IS NULL AND [NextAttemptAtUtc] IS NULL)");
                    table.ForeignKey(
                        name: "FK_TripSafety_SafeCheckInOutbox_SafeCheckIns_CheckInId",
                        column: x => x.CheckInId,
                        principalTable: "TripSafety_SafeCheckIns",
                        principalColumn: "CheckInId");
                    table.ForeignKey(
                        name: "FK_TripSafety_SafeCheckInOutbox_Users_RecipientUserId",
                        column: x => x.RecipientUserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "TripSafety_TripMemoryMedia",
                columns: table => new
                {
                    OperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MemoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ContentLength = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    State = table.Column<int>(type: "int", nullable: false),
                    ObjectKey = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ObjectVersion = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CleanupClaimId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CleanedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TripSafety_TripMemoryMedia", x => x.OperationId);
                    table.CheckConstraint("CK_TripSafety_TripMemoryMedia_Size", "[ContentLength] > 0");
                    table.CheckConstraint("CK_TripSafety_TripMemoryMedia_State", "([State] = 0 AND [ObjectKey] IS NULL AND [ObjectVersion] IS NULL AND [CleanupClaimId] IS NULL AND [CleanedAtUtc] IS NULL) OR ([State] = 1 AND [ObjectKey] IS NOT NULL AND [ObjectVersion] IS NOT NULL AND [CleanupClaimId] IS NULL AND [CleanedAtUtc] IS NULL) OR ([State] IN (2, 3) AND [ObjectKey] IS NOT NULL AND [ObjectVersion] IS NOT NULL AND [CleanupClaimId] IS NOT NULL AND (([State] = 2 AND [CleanedAtUtc] IS NULL) OR ([State] = 3 AND [CleanedAtUtc] IS NOT NULL)))");
                    table.ForeignKey(
                        name: "FK_TripSafety_TripMemoryMedia_TripSafety_TripMemories_MemoryId",
                        column: x => x.MemoryId,
                        principalTable: "TripSafety_TripMemories",
                        principalColumn: "MemoryId");
                });

            migrationBuilder.CreateTable(
                name: "TripSafety_NotificationRecipients",
                columns: table => new
                {
                    EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ReadAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    State = table.Column<int>(type: "int", nullable: false),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    NextAttemptAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LeaseToken = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LeaseUntilUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastOutcome = table.Column<string>(type: "nvarchar(48)", maxLength: 48, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TripSafety_NotificationRecipients", x => new { x.EventId, x.UserId });
                    table.CheckConstraint("CK_MS08_NotificationRecipient_Lease", "([LeaseToken] IS NULL AND [LeaseUntilUtc] IS NULL) OR ([State] = 0 AND [LeaseToken] IS NOT NULL AND [LeaseUntilUtc] IS NOT NULL)");
                    table.CheckConstraint("CK_MS08_NotificationRecipient_State", "[Attempts] >= 0 AND (([State] = 0 AND [CompletedAtUtc] IS NULL) OR ([State] BETWEEN 1 AND 3 AND [CompletedAtUtc] IS NOT NULL AND [NextAttemptAtUtc] IS NULL))");
                    table.ForeignKey(
                        name: "FK_TripSafety_NotificationRecipients_TripSafety_NotificationEvents_EventId",
                        column: x => x.EventId,
                        principalTable: "TripSafety_NotificationEvents",
                        principalColumn: "EventId");
                    table.ForeignKey(
                        name: "FK_TripSafety_NotificationRecipients_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "TripSafety_NotificationDeliveryAudit",
                columns: table => new
                {
                    AuditId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Attempt = table.Column<int>(type: "int", nullable: false),
                    Outcome = table.Column<string>(type: "varchar(48)", unicode: false, maxLength: 48, nullable: false),
                    RecordedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TripSafety_NotificationDeliveryAudit", x => x.AuditId);
                    table.ForeignKey(
                        name: "FK_TripSafety_NotificationDeliveryAudit_TripSafety_NotificationRecipients_EventId_UserId",
                        columns: x => new { x.EventId, x.UserId },
                        principalTable: "TripSafety_NotificationRecipients",
                        principalColumns: new[] { "EventId", "UserId" });
                });

            migrationBuilder.Sql(
                "EXEC(N'ALTER TABLE [TripSafety_HazardReports] ADD CONSTRAINT [CK_TripSafety_HazardReports_Coordinates] CHECK (([Latitude] IS NULL AND [Longitude] IS NULL) OR ([Latitude] BETWEEN -90 AND 90 AND [Longitude] BETWEEN -180 AND 180))');");

            migrationBuilder.CreateIndex(
                name: "UX_VoucherIssuances_Inventory",
                table: "SampleVoucherIssuances",
                column: "InventoryId",
                unique: true,
                filter: "[InventoryId] IS NOT NULL");

            migrationBuilder.Sql(
                "EXEC(N'ALTER TABLE [SampleVoucherIssuances] ADD CONSTRAINT [CK_VoucherIssuances_Attempts] CHECK ([AttemptCount] >= 0)');");

            migrationBuilder.AddCheckConstraint(
                name: "CK_VoucherIssuances_Status",
                table: "SampleVoucherIssuances",
                sql: "[Status] IN (1, 2, 3, 4, 5)");

            migrationBuilder.Sql(
                "EXEC(N'ALTER TABLE [SampleVoucherInventory] ADD CONSTRAINT [CK_VoucherInventory_Expiry] CHECK ([ExpiresAtUtc] > [AddedAtUtc])');");

            migrationBuilder.CreateIndex(
                name: "UX_CoinLedgerEntries_CorrectionOf",
                table: "CoinLedgerEntries",
                column: "CorrectionOfEntryId",
                unique: true,
                filter: "[CorrectionOfEntryId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_CoinLedgerEntries_Wallet_NonRewardSource",
                table: "CoinLedgerEntries",
                columns: new[] { "WalletId", "SourceRecordId" },
                unique: true,
                filter: "[RewardType] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TripPlanCopyAttributions_SourceTripPlanId",
                table: "TripPlanCopyAttributions",
                column: "SourceTripPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_TripSafety_MemberRevocationOutbox_Pending",
                table: "TripSafety_MemberRevocationOutbox",
                columns: new[] { "IsDelivered", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_TripSafety_MemberRevocationOutbox_MemberId",
                table: "TripSafety_MemberRevocationOutbox",
                column: "MemberId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TripSafety_NotificationAudienceReceipts_ActorId",
                table: "TripSafety_NotificationAudienceReceipts",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_TripSafety_NotificationAudienceReceipts_ExpiresAtUtc",
                table: "TripSafety_NotificationAudienceReceipts",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_TripSafety_NotificationDeliveryAudit_EventId_UserId",
                table: "TripSafety_NotificationDeliveryAudit",
                columns: new[] { "EventId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_TripSafety_NotificationDevices_TokenFingerprint",
                table: "TripSafety_NotificationDevices",
                column: "TokenFingerprint",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TripSafety_NotificationDevices_UserId_RevokedAtUtc",
                table: "TripSafety_NotificationDevices",
                columns: new[] { "UserId", "RevokedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TripSafety_NotificationEvents_JobId",
                table: "TripSafety_NotificationEvents",
                column: "JobId",
                unique: true,
                filter: "[JobId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TripSafety_NotificationJobRecipients_UserId",
                table: "TripSafety_NotificationJobRecipients",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_TripSafety_NotificationJobs_ActorId",
                table: "TripSafety_NotificationJobs",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_TripSafety_NotificationJobs_SessionId_TripPlanId",
                table: "TripSafety_NotificationJobs",
                columns: new[] { "SessionId", "TripPlanId" });

            migrationBuilder.CreateIndex(
                name: "IX_TripSafety_NotificationJobs_State_DueAtUtc",
                table: "TripSafety_NotificationJobs",
                columns: new[] { "State", "DueAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TripSafety_NotificationJobs_TripPlanId_SessionId_ActorId",
                table: "TripSafety_NotificationJobs",
                columns: new[] { "TripPlanId", "SessionId", "ActorId" },
                unique: true,
                filter: "[Kind] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_TripSafety_NotificationOperations_ResourceId_RecordedAtUtc",
                table: "TripSafety_NotificationOperations",
                columns: new[] { "ResourceId", "RecordedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TripSafety_NotificationRecipients_NextAttemptAtUtc_LeaseUntilUtc",
                table: "TripSafety_NotificationRecipients",
                columns: new[] { "NextAttemptAtUtc", "LeaseUntilUtc" },
                filter: "[State] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_TripSafety_NotificationRecipients_UserId_ReadAtUtc_EventId",
                table: "TripSafety_NotificationRecipients",
                columns: new[] { "UserId", "ReadAtUtc", "EventId" });

            migrationBuilder.CreateIndex(
                name: "IX_TripSafety_NotificationTokenBindings_DeviceId",
                table: "TripSafety_NotificationTokenBindings",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_TripSafety_NotificationTokenBindings_UserId",
                table: "TripSafety_NotificationTokenBindings",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_TripSafety_SafeCheckInOutbox_PendingDue",
                table: "TripSafety_SafeCheckInOutbox",
                columns: new[] { "NextAttemptAtUtc", "CreatedAtUtc", "Id" },
                filter: "[IsDelivered] = 0 AND [DeadLetteredAtUtc] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TripSafety_SafeCheckInOutbox_PendingLeaseExpiry",
                table: "TripSafety_SafeCheckInOutbox",
                columns: new[] { "LeaseUntilUtc", "Id" },
                filter: "[IsDelivered] = 0 AND [DeadLetteredAtUtc] IS NULL AND [LeaseUntilUtc] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TripSafety_SafeCheckInOutbox_RecipientUserId",
                table: "TripSafety_SafeCheckInOutbox",
                column: "RecipientUserId");

            migrationBuilder.CreateIndex(
                name: "UX_TripSafety_SafeCheckInOutbox_CheckInId",
                table: "TripSafety_SafeCheckInOutbox",
                column: "CheckInId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TripSafety_SafeCheckIns_SessionId_TripPlanId",
                table: "TripSafety_SafeCheckIns",
                columns: new[] { "SessionId", "TripPlanId" });

            migrationBuilder.CreateIndex(
                name: "IX_TripSafety_SafeCheckIns_TripPlanId",
                table: "TripSafety_SafeCheckIns",
                column: "TripPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_TripSafety_SafeCheckIns_UserId",
                table: "TripSafety_SafeCheckIns",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "UX_TripSafety_SafeCheckIns_SessionId_UserId",
                table: "TripSafety_SafeCheckIns",
                columns: new[] { "SessionId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TripSafety_TripMemories_OwnerUserId",
                table: "TripSafety_TripMemories",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TripSafety_TripMemories_TripPlanId_OwnerUserId_CreatedAtUtc",
                table: "TripSafety_TripMemories",
                columns: new[] { "TripPlanId", "OwnerUserId", "CreatedAtUtc" },
                filter: "[DeletedAtUtc] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TripSafety_TripMemories_TripPlanId_OwnerUserId_CreateOperationId",
                table: "TripSafety_TripMemories",
                columns: new[] { "TripPlanId", "OwnerUserId", "CreateOperationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TripSafety_TripMemories_TripStopId_TripPlanId",
                table: "TripSafety_TripMemories",
                columns: new[] { "TripStopId", "TripPlanId" });

            migrationBuilder.CreateIndex(
                name: "IX_TripSafety_TripMemoryMedia_MemoryId",
                table: "TripSafety_TripMemoryMedia",
                column: "MemoryId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TripSafety_TripMemoryMedia_ObjectKey",
                table: "TripSafety_TripMemoryMedia",
                column: "ObjectKey",
                unique: true,
                filter: "[ObjectKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TripSafety_TripMemoryMedia_State_CreatedAtUtc",
                table: "TripSafety_TripMemoryMedia",
                columns: new[] { "State", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_UserRoleAssignments_AssignedAtUtc",
                table: "UserRoleAssignments",
                column: "AssignedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_UserRoleAssignments_UserId_Status",
                table: "UserRoleAssignments",
                columns: new[] { "UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "UX_UserRoleAssignments_Active_UserId_Role",
                table: "UserRoleAssignments",
                columns: new[] { "UserId", "Role" },
                unique: true,
                filter: "[Status] = N'Active'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TripPlanCopyAttributions");

            migrationBuilder.DropTable(
                name: "TripSafety_MemberRevocationOutbox");

            migrationBuilder.DropTable(
                name: "TripSafety_NotificationAudienceReceipts");

            migrationBuilder.DropTable(
                name: "TripSafety_NotificationDeliveryAudit");

            migrationBuilder.DropTable(
                name: "TripSafety_NotificationJobRecipients");

            migrationBuilder.DropTable(
                name: "TripSafety_NotificationOperations");

            migrationBuilder.DropTable(
                name: "TripSafety_NotificationPreferences");

            migrationBuilder.DropTable(
                name: "TripSafety_NotificationTokenBindings");

            migrationBuilder.DropTable(
                name: "TripSafety_SafeCheckInOutbox");

            migrationBuilder.DropTable(
                name: "TripSafety_TripMemoryMedia");

            migrationBuilder.DropTable(
                name: "UserRoleAssignments");

            migrationBuilder.DropTable(
                name: "TripSafety_NotificationRecipients");

            migrationBuilder.DropTable(
                name: "TripSafety_NotificationDevices");

            migrationBuilder.DropTable(
                name: "TripSafety_SafeCheckIns");

            migrationBuilder.DropTable(
                name: "TripSafety_TripMemories");

            migrationBuilder.DropTable(
                name: "TripSafety_NotificationEvents");

            migrationBuilder.DropTable(
                name: "TripSafety_NotificationJobs");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_TripStops_Id_TripPlanId",
                table: "TripStops");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_TripSafety_LiveTripSessions_SessionId_TripPlanId",
                table: "TripSafety_LiveTripSessions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TripSafety_HazardReports_Coordinates",
                table: "TripSafety_HazardReports");

            migrationBuilder.DropIndex(
                name: "UX_VoucherIssuances_Inventory",
                table: "SampleVoucherIssuances");

            migrationBuilder.DropCheckConstraint(
                name: "CK_VoucherIssuances_Attempts",
                table: "SampleVoucherIssuances");

            migrationBuilder.DropCheckConstraint(
                name: "CK_VoucherIssuances_Status",
                table: "SampleVoucherIssuances");

            migrationBuilder.DropCheckConstraint(
                name: "CK_VoucherInventory_Expiry",
                table: "SampleVoucherInventory");

            migrationBuilder.DropIndex(
                name: "UX_CoinLedgerEntries_CorrectionOf",
                table: "CoinLedgerEntries");

            migrationBuilder.DropIndex(
                name: "UX_CoinLedgerEntries_Wallet_NonRewardSource",
                table: "CoinLedgerEntries");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "TripSafety_HazardReports");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "TripSafety_HazardReports");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "TripSafety_HazardReports");

            migrationBuilder.DropColumn(
                name: "PhotoAssetReference",
                table: "TripSafety_HazardReports");

            migrationBuilder.DropColumn(
                name: "DecisionActorUserId",
                table: "SampleVoucherRedemptionRequests");

            migrationBuilder.DropColumn(
                name: "DecisionReason",
                table: "SampleVoucherRedemptionRequests");

            migrationBuilder.DropColumn(
                name: "OwnerNameSnapshot",
                table: "SampleVoucherRedemptionRequests");

            migrationBuilder.DropColumn(
                name: "AttemptCount",
                table: "SampleVoucherIssuances");

            migrationBuilder.DropColumn(
                name: "ExpiresAtUtc",
                table: "SampleVoucherIssuances");

            migrationBuilder.DropColumn(
                name: "LastErrorCode",
                table: "SampleVoucherIssuances");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "SampleVoucherIssuances");

            migrationBuilder.DropColumn(
                name: "UseReason",
                table: "SampleVoucherIssuances");

            migrationBuilder.DropColumn(
                name: "UsedAtUtc",
                table: "SampleVoucherIssuances");

            migrationBuilder.DropColumn(
                name: "UsedByActorUserId",
                table: "SampleVoucherIssuances");

            migrationBuilder.DropColumn(
                name: "AddedAtUtc",
                table: "SampleVoucherInventory");

            migrationBuilder.DropColumn(
                name: "AddedByAdminUserId",
                table: "SampleVoucherInventory");

            migrationBuilder.DropColumn(
                name: "ExpiresAtUtc",
                table: "SampleVoucherInventory");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "SampleVoucherInventory");

            migrationBuilder.DropColumn(
                name: "SampleImageReference",
                table: "SampleVoucherInventory");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "CoinRuleVersions");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "CoinRuleVersions");

            migrationBuilder.DropColumn(
                name: "EffectiveFromUtc",
                table: "CoinRuleVersions");

            migrationBuilder.DropColumn(
                name: "EffectiveUntilUtc",
                table: "CoinRuleVersions");

            migrationBuilder.DropColumn(
                name: "ActorUserId",
                table: "CoinLedgerEntries");

            migrationBuilder.DropColumn(
                name: "Reason",
                table: "CoinLedgerEntries");

            migrationBuilder.AlterColumn<Guid>(
                name: "InventoryId",
                table: "SampleVoucherIssuances",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "UX_VoucherIssuances_Inventory",
                table: "SampleVoucherIssuances",
                column: "InventoryId",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_VoucherIssuances_Status",
                table: "SampleVoucherIssuances",
                sql: "[Status] IN (1, 2, 3)");
        }
    }
}
