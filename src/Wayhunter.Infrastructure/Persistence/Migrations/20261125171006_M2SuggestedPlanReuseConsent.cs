using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayhunter.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class M2SuggestedPlanReuseConsent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AllowsSuggestedPlanReuse",
                table: "Trips",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "RequestKeyHash",
                table: "TripPlanCopyAttributions",
                type: "varchar(64)",
                unicode: false,
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequesterUserId",
                table: "TripPlanCopyAttributions",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TripPlanCopyAttributions_RequesterUserId_RequestKeyHash",
                table: "TripPlanCopyAttributions",
                columns: new[] { "RequesterUserId", "RequestKeyHash" },
                unique: true,
                filter: "[RequesterUserId] IS NOT NULL AND [RequestKeyHash] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TripPlanCopies_RequestIdentity",
                table: "TripPlanCopyAttributions",
                sql: "([RequesterUserId] IS NULL AND [RequestKeyHash] IS NULL) OR ([RequesterUserId] IS NOT NULL AND [RequestKeyHash] IS NOT NULL AND LEN([RequestKeyHash]) = 64)");

            migrationBuilder.AddForeignKey(
                name: "FK_TripPlanCopyAttributions_Users_RequesterUserId",
                table: "TripPlanCopyAttributions",
                column: "RequesterUserId",
                principalTable: "Users",
                principalColumn: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TripPlanCopyAttributions_Users_RequesterUserId",
                table: "TripPlanCopyAttributions");

            migrationBuilder.DropIndex(
                name: "IX_TripPlanCopyAttributions_RequesterUserId_RequestKeyHash",
                table: "TripPlanCopyAttributions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TripPlanCopies_RequestIdentity",
                table: "TripPlanCopyAttributions");

            migrationBuilder.DropColumn(
                name: "AllowsSuggestedPlanReuse",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "RequestKeyHash",
                table: "TripPlanCopyAttributions");

            migrationBuilder.DropColumn(
                name: "RequesterUserId",
                table: "TripPlanCopyAttributions");
        }
    }
}
