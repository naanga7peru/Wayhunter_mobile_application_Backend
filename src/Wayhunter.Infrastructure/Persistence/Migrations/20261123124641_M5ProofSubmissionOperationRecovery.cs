using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayhunter.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class M5ProofSubmissionOperationRecovery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GameProofSubmissionOperations",
                columns: table => new
                {
                    SubmissionOperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuthenticatedPlayerUserId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    GameId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttemptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StepNumber = table.Column<int>(type: "int", nullable: false),
                    CaptureSessionId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    MediaProviderNamespace = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CanonicalMediaId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    State = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    NextReconciliationAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ClaimToken = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    LeaseExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ProviderConsumptionConfirmedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ProviderReceiptId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EvidenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastErrorCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    RecoveryAttemptCount = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameProofSubmissionOperations", x => x.SubmissionOperationId);
                    table.CheckConstraint("CK_GameProofOperations_RecoveryAttempts", "[RecoveryAttemptCount] >= 0");
                    table.CheckConstraint("CK_GameProofOperations_State", "[State] IN (0,1,2,3,4,5,6)");
                    table.CheckConstraint("CK_GameProofOperations_Step", "[StepNumber] >= 1");
                    table.ForeignKey(
                        name: "FK_GameProofSubmissionOperations_GameAttempts_AttemptId_GameId_AuthenticatedPlayerUserId",
                        columns: x => new { x.AttemptId, x.GameId, x.AuthenticatedPlayerUserId },
                        principalTable: "GameAttempts",
                        principalColumns: new[] { "Id", "GameId", "PlayerUserId" });
                });

            migrationBuilder.CreateIndex(
                name: "IX_GameProofOperations_Reconciliation",
                table: "GameProofSubmissionOperations",
                columns: new[] { "State", "NextReconciliationAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_GameProofSubmissionOperations_AttemptId_GameId_AuthenticatedPlayerUserId",
                table: "GameProofSubmissionOperations",
                columns: new[] { "AttemptId", "GameId", "AuthenticatedPlayerUserId" });

            migrationBuilder.CreateIndex(
                name: "UX_GameProofOperations_Provider_Capture",
                table: "GameProofSubmissionOperations",
                columns: new[] { "MediaProviderNamespace", "CaptureSessionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_GameProofOperations_Provider_Media",
                table: "GameProofSubmissionOperations",
                columns: new[] { "MediaProviderNamespace", "CanonicalMediaId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GameProofSubmissionOperations");
        }
    }
}
