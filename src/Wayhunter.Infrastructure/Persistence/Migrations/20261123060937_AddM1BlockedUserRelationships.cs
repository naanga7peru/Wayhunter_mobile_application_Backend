using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayhunter.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddM1BlockedUserRelationships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BlockedUserRelationships",
                columns: table => new
                {
                    BlockerUserId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    BlockedUserId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BlockedUserRelationships", x => new { x.BlockerUserId, x.BlockedUserId });
                    table.CheckConstraint("CK_BlockedUserRelationships_NoSelfBlock", "[BlockerUserId] <> [BlockedUserId]");
                    table.ForeignKey(
                        name: "FK_BlockedUserRelationships_Users_BlockedUserId",
                        column: x => x.BlockedUserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                    table.ForeignKey(
                        name: "FK_BlockedUserRelationships_Users_BlockerUserId",
                        column: x => x.BlockerUserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_BlockedUserRelationships_BlockedUserId_BlockerUserId",
                table: "BlockedUserRelationships",
                columns: new[] { "BlockedUserId", "BlockerUserId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BlockedUserRelationships");
        }
    }
}
