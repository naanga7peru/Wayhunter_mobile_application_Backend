using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayhunter.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class M4HunterWriteWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CaptureSessionId",
                table: "Hunter_Evidence",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OwnerUserId",
                table: "Hunter_Evidence",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderAssetIdHash",
                table: "Hunter_Evidence",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderReceiptDigest",
                table: "Hunter_Evidence",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ProviderVerifiedAtUtc",
                table: "Hunter_Evidence",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Hunter_CaptureSessions_Id_Submission",
                table: "Hunter_CaptureSessions",
                columns: new[] { "Id", "HunterSubmissionId" });

            migrationBuilder.CreateTable(
                name: "Hunter_RequestIdempotency",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerUserId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Operation = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    KeyHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    RequestDigest = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ResourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Hunter_RequestIdempotency", x => x.Id);
                    table.CheckConstraint("CK_Hunter_RequestIdempotency_DigestLengths", "LEN([KeyHash]) = 64 AND LEN([RequestDigest]) = 64");
                    table.ForeignKey(
                        name: "FK_Hunter_RequestIdempotency_Users",
                        column: x => x.OwnerUserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Hunter_Evidence_CaptureSessionId_HunterSubmissionId",
                table: "Hunter_Evidence",
                columns: new[] { "CaptureSessionId", "HunterSubmissionId" });

            migrationBuilder.CreateIndex(
                name: "IX_Hunter_Evidence_HunterSubmissionId_OwnerUserId",
                table: "Hunter_Evidence",
                columns: new[] { "HunterSubmissionId", "OwnerUserId" });

            migrationBuilder.CreateIndex(
                name: "UX_Hunter_Evidence_CaptureSession",
                table: "Hunter_Evidence",
                column: "CaptureSessionId",
                unique: true,
                filter: "[CaptureSessionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_Hunter_Evidence_ProviderAsset",
                table: "Hunter_Evidence",
                column: "ProviderAssetIdHash",
                unique: true,
                filter: "[ProviderAssetIdHash] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_Hunter_Evidence_Verified_Submission_Type",
                table: "Hunter_Evidence",
                columns: new[] { "HunterSubmissionId", "EvidenceType" },
                unique: true,
                filter: "[ProviderVerifiedAtUtc] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Hunter_Evidence_ProviderHashLengths",
                table: "Hunter_Evidence",
                sql: "([ProviderAssetIdHash] IS NULL OR LEN([ProviderAssetIdHash]) = 64) AND ([ProviderReceiptDigest] IS NULL OR LEN([ProviderReceiptDigest]) = 64)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Hunter_Evidence_VerifiedBinding",
                table: "Hunter_Evidence",
                sql: "[ProviderVerifiedAtUtc] IS NULL OR ([CaptureSessionId] IS NOT NULL AND [OwnerUserId] IS NOT NULL AND [ProviderAssetIdHash] IS NOT NULL AND [ProviderReceiptDigest] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "UX_Hunter_RequestIdempotency_Owner_Operation_Key",
                table: "Hunter_RequestIdempotency",
                columns: new[] { "OwnerUserId", "Operation", "KeyHash" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Hunter_Evidence_CaptureSession_Owner",
                table: "Hunter_Evidence",
                columns: new[] { "CaptureSessionId", "HunterSubmissionId" },
                principalTable: "Hunter_CaptureSessions",
                principalColumns: new[] { "Id", "HunterSubmissionId" });

            migrationBuilder.AddForeignKey(
                name: "FK_Hunter_Evidence_Submission_Owner",
                table: "Hunter_Evidence",
                columns: new[] { "HunterSubmissionId", "OwnerUserId" },
                principalTable: "Hunter_Submissions",
                principalColumns: new[] { "Id", "UserId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Hunter_Evidence_CaptureSession_Owner",
                table: "Hunter_Evidence");

            migrationBuilder.DropForeignKey(
                name: "FK_Hunter_Evidence_Submission_Owner",
                table: "Hunter_Evidence");

            migrationBuilder.DropTable(
                name: "Hunter_RequestIdempotency");

            migrationBuilder.DropIndex(
                name: "IX_Hunter_Evidence_CaptureSessionId_HunterSubmissionId",
                table: "Hunter_Evidence");

            migrationBuilder.DropIndex(
                name: "IX_Hunter_Evidence_HunterSubmissionId_OwnerUserId",
                table: "Hunter_Evidence");

            migrationBuilder.DropIndex(
                name: "UX_Hunter_Evidence_CaptureSession",
                table: "Hunter_Evidence");

            migrationBuilder.DropIndex(
                name: "UX_Hunter_Evidence_ProviderAsset",
                table: "Hunter_Evidence");

            migrationBuilder.DropIndex(
                name: "UX_Hunter_Evidence_Verified_Submission_Type",
                table: "Hunter_Evidence");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Hunter_Evidence_ProviderHashLengths",
                table: "Hunter_Evidence");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Hunter_Evidence_VerifiedBinding",
                table: "Hunter_Evidence");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Hunter_CaptureSessions_Id_Submission",
                table: "Hunter_CaptureSessions");

            migrationBuilder.DropColumn(
                name: "CaptureSessionId",
                table: "Hunter_Evidence");

            migrationBuilder.DropColumn(
                name: "OwnerUserId",
                table: "Hunter_Evidence");

            migrationBuilder.DropColumn(
                name: "ProviderAssetIdHash",
                table: "Hunter_Evidence");

            migrationBuilder.DropColumn(
                name: "ProviderReceiptDigest",
                table: "Hunter_Evidence");

            migrationBuilder.DropColumn(
                name: "ProviderVerifiedAtUtc",
                table: "Hunter_Evidence");
        }
    }
}
