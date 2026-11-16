
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
     });
    

        // Primary key
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        // Reporter identity
        builder.Property(x => x.ReporterUserId)
            .HasMaxLength(256)
            .IsRequired();

        // Hazard lifecycle
        builder.Property(x => x.Status)
            .HasConversion<int>()
            .IsRequired();

        // Trusted and media timestamps
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

        // Optional reviewer identities
        builder.Property(x => x.VerifiedByUserId)
            .HasMaxLength(256)
            .IsRequired(false);

        builder.Property(x => x.ResolvedByUserId)
            .HasMaxLength(256)
            .IsRequired(false);

        // Domain currently defines no maximum length
        // for ResolutionReason; do not invent one here.
        builder.Property(x => x.ResolutionReason)
            .IsRequired(false);

        // M1: Reporter relationship
        builder.HasOne<UserAccount>()
            .WithMany()
            .HasForeignKey(x => x.ReporterUserId)
            .HasPrincipalKey(x => x.UserId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName(
                "FK_TripSafety_HazardReports_Users_ReporterUserId");

        // M1: Verifier relationship
        builder.HasOne<UserAccount>()
            .WithMany()
            .HasForeignKey(x => x.VerifiedByUserId)
            .HasPrincipalKey(x => x.UserId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName(
                "FK_TripSafety_HazardReports_Users_VerifiedByUserId");

        // M1: Resolver relationship
        builder.HasOne<UserAccount>()
            .WithMany()
            .HasForeignKey(x => x.ResolvedByUserId)
            .HasPrincipalKey(x => x.UserId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName(
                "FK_TripSafety_HazardReports_Users_ResolvedByUserId");

        // Expiry worker / active hazard queries.
        builder.HasIndex(x => new
        {
            x.Status,
            x.ExpiresAtUtc
        })
        .HasDatabaseName(
            "IX_TripSafety_HazardReports_Status_ExpiresAtUtc");

        // Reporter history queries.
        builder.HasIndex(x => new
        {
            x.ReporterUserId,
            x.ReportedAtUtc
        })
        .HasDatabaseName(
            "IX_TripSafety_HazardReports_Reporter_ReportedAtUtc");
    }
}
