using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayhunter.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class M5GameDraftCreateIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GameDraftCreateOperations",
                columns: table => new
                {
                    CreatorUserId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    PayloadHash = table.Column<byte[]>(type: "binary(32)", nullable: false),
                    GameId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameDraftCreateOperations", x => new { x.CreatorUserId, x.IdempotencyKey });
                    table.ForeignKey(
                        name: "FK_GameDraftCreateOperations_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_GameDraftCreateOperations_Users_CreatorUserId",
                        column: x => x.CreatorUserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_GameDraftCreateOperations_CreatedAt",
                table: "GameDraftCreateOperations",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "UX_GameDraftCreateOperations_Game",
                table: "GameDraftCreateOperations",
                column: "GameId",
                unique: true,
                filter: "[GameId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GameDraftCreateOperations");
        }
    }
}
