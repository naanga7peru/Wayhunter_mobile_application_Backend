
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Wayhunter.Domain.Modules.TripSafety.Entities;

namespace Wayhunter.Infrastructure.Persistence.Configurations.TripSafety;

public sealed class HazardReportConfiguration
    : IEntityTypeConfiguration<HazardReport>
{
    public void Configure(
        EntityTypeBuilder<HazardReport> builder)
    {
        builder.ToTable(
            "TripSafety_HazardReports",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_TripSafety_HazardReports_Status",
                    "[Status] IN (1, 2, 3, 4)");

                table.HasCheckConstraint(
                    "CK_TripSafety_HazardReports_Expiry",
                    "[ExpiresAtUtc] = DATEADD(hour, 24, [ReportedAtUtc])");

                table.HasCheckConstraint(
                    "CK_TripSafety_HazardReports_Verification",
                    "([VerifiedAtUtc] IS NULL AND " +
                    "[VerifiedByUserId] IS NULL) OR " +
                    "([VerifiedAtUtc] IS NOT NULL AND " +
                    "[VerifiedByUserId] IS NOT NULL AND " +
                    "[VerifiedAtUtc] >= [ReportedAtUtc])");

                table.HasCheckConstraint(
                    "CK_TripSafety_HazardReports_Resolution",
                    "([ResolvedAtUtc] IS NULL AND " +
                    "[ResolvedByUserId] IS NULL AND " +
                    "[ResolutionReason] IS NULL) OR " +
                    "([ResolvedAtUtc] IS NOT NULL AND " +
                    "[ResolvedByUserId] IS NOT NULL AND " +
                    "[ResolutionReason] IS NOT NULL AND " +
                    "[ResolvedAtUtc] >= [ReportedAtUtc])");

                table.HasCheckConstraint(
                    "CK_TripSafety_HazardReports_ExpiryTimestamp",
                    "[ExpiredAtUtc] IS NULL OR " +
                    "[ExpiredAtUtc] >= [ExpiresAtUtc]");

                table.HasCheckConstraint(
                    "CK_TripSafety_HazardReports_VerifiedStatus",
                    "[Status] <> 2 OR " +
                    "([VerifiedAtUtc] IS NOT NULL AND " +
                    "[VerifiedByUserId] IS NOT NULL)");

                table.HasCheckConstraint(
                    "CK_TripSafety_HazardReports_ResolvedStatus",
                    "[Status] <> 3 OR " +
                    "([ResolvedAtUtc] IS NOT NULL AND " +
                    "[ResolvedByUserId] IS NOT NULL AND " +
                    "[ResolutionReason] IS NOT NULL)");

                table.HasCheckConstraint(
                    "CK_TripSafety_HazardReports_ExpiredStatus",
                    "[Status] <> 4 OR [ExpiredAtUtc] IS NOT NULL");

                // Proposed schema extension.
                table.HasCheckConstraint(
                    "CK_TripSafety_HazardReports_Coordinates",
                    "([Latitude] IS NULL AND [Longitude] IS NULL) " +
                    "OR ([Latitude] BETWEEN -90 AND 90 AND " +
                    "[Longitude] BETWEEN -180 AND 180)");
            });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.Property(x => x.ReporterUserId)
            .HasMaxLength(256)
            .IsRequired();

        // Use status for optimistic concurrency:
        // competing Verify/Resolve/Expire cannot blindly
        // overwrite a changed lifecycle state.
        builder.Property(x => x.Status)
            .HasConversion<int>()
            .IsConcurrencyToken()
            .IsRequired();

        builder.Property(x => x.ReportedAtUtc)
            .HasColumnType("datetimeoffset")
            .IsRequired();

        builder.Property(x => x.PhotoCapturedAtUtc)
            .HasColumnType("datetimeoffset")
            .IsRequired();

        builder.Property(x => x.ExpiresAtUtc)
            .HasColumnType("datetimeoffset")
            .IsRequired();

        builder.Property(x => x.VerifiedAtUtc)
            .HasColumnType("datetimeoffset")
            .IsRequired(false);

        builder.Property(x => x.ResolvedAtUtc)
            .HasColumnType("datetimeoffset")
            .IsRequired(false);

        builder.Property(x => x.ExpiredAtUtc)
            .HasColumnType("datetimeoffset")
            .IsRequired(false);

        builder.Property(x => x.VerifiedByUserId)
            .HasMaxLength(256)
            .IsRequired(false);

        builder.Property(x => x.ResolvedByUserId)
            .HasMaxLength(256)
            .IsRequired(false);

        builder.Property(x => x.ResolutionReason)
            .IsRequired(false);

        // New nullable columns preserve legacy rows.
        // Requires coordinated shared SQL migration.
        builder.Property(x => x.Latitude)
            .HasColumnType("float")
            .IsRequired(false);

        builder.Property(x => x.Longitude)
            .HasColumnType("float")
            .IsRequired(false);

        builder.Property(x => x.Description)
            .HasMaxLength(1000)
            .IsRequired(false);

        builder.Property(x => x.PhotoAssetReference)
            .HasMaxLength(512)
            .IsRequired(false);

        builder.HasOne<UserAccount>()
            .WithMany()
            .HasForeignKey(x => x.ReporterUserId)
            .HasPrincipalKey(x => x.UserId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName(
                "FK_TripSafety_HazardReports_Users_ReporterUserId");

        builder.HasOne<UserAccount>()
            .WithMany()
            .HasForeignKey(x => x.VerifiedByUserId)
            .HasPrincipalKey(x => x.UserId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName(
                "FK_TripSafety_HazardReports_Users_VerifiedByUserId");

        builder.HasOne<UserAccount>()
            .WithMany()
            .HasForeignKey(x => x.ResolvedByUserId)
            .HasPrincipalKey(x => x.UserId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName(
                "FK_TripSafety_HazardReports_Users_ResolvedByUserId");

        builder.HasIndex(x => new
        {
            x.Status,
            x.ExpiresAtUtc
        })
        .HasDatabaseName(
            "IX_TripSafety_HazardReports_Status_ExpiresAtUtc");

        builder.HasIndex(x => new
        {
            x.ReporterUserId,
            x.ReportedAtUtc
        })
        .HasDatabaseName(
            "IX_TripSafety_HazardReports_Reporter_ReportedAtUtc");
    }
}
