using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayhunter.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class M4EnforcePositiveGpsAccuracy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Hunter_Submissions_GpsAccuracy",
                table: "Hunter_Submissions");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Hunter_Submissions_GpsAccuracy",
                table: "Hunter_Submissions",
                sql: "[GpsAccuracyMeters] IS NULL OR [GpsAccuracyMeters] > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Hunter_Submissions_GpsAccuracy",
                table: "Hunter_Submissions");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Hunter_Submissions_GpsAccuracy",
                table: "Hunter_Submissions",
                sql: "[GpsAccuracyMeters] IS NULL OR [GpsAccuracyMeters] >= 0");
        }
    }
}
