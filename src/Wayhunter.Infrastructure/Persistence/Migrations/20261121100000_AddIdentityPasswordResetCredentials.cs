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
                    UserId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    TokenHash = table.Column<string>(type: "char(64)", maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ConsumedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RevokedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdentityPasswordResetCredentials", x => x.CredentialId);
                    table.CheckConstraint(
                        "CK_IdentityPasswordResetCredentials_Expiry",
                        "[ExpiresAtUtc] > [CreatedAtUtc]");
                    table.ForeignKey(
                        name: "FK_IdentityPasswordResetCredentials_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.NoAction);
                });

            migrationBuilder.CreateIndex(
                name: "UX_IdentityPasswordResetCredentials_TokenHash",
                table: "IdentityPasswordResetCredentials",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IdentityPasswordResetCredentials_User_Expiry",
                table: "IdentityPasswordResetCredentials",
                columns: new[] { "UserId", "ExpiresAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IdentityPasswordResetCredentials");
        }
    }
}
