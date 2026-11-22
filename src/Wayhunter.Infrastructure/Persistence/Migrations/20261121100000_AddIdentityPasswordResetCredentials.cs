using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayhunter.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIdentityPasswordResetCredentials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IdentityPasswordResetCredentials",
                columns: table => new
                {
                    CredentialId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConsumedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RevokedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    TokenHash = table.Column<string>(type: "char(64)", maxLength: 64, nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdentityPasswordResetCredentials", x => x.CredentialId);
                    table.CheckConstraint("CK_IdentityPasswordResetCredentials_Expiry", "[ExpiresAtUtc] > [CreatedAtUtc]");
                    table.ForeignKey(
                        name: "FK_IdentityPasswordResetCredentials_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_IdentityPasswordResetCredentials_User_Expiry",
                table: "IdentityPasswordResetCredentials",
                columns: new[] { "UserId", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_IdentityPasswordResetCredentials_TokenHash",
                table: "IdentityPasswordResetCredentials",
                column: "TokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IdentityPasswordResetCredentials");
        }
    }
}
