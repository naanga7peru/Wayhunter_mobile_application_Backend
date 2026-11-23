
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wayhunter.Domain.Modules.HunterPlaces.Entities;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;

namespace Wayhunter.Infrastructure.Persistence.Configurations.HunterPlaces;

public sealed class HunterSubmissionConfiguration
    : IEntityTypeConfiguration<HunterSubmission>
{
    public void Configure(
        EntityTypeBuilder<HunterSubmission> builder)
    {
        builder.ToTable("Hunter_Submissions", table =>
        {
            table.HasCheckConstraint(
                "CK_Hunter_Submissions_Status",
                "[Status] IN (0, 1, 2, 3, 4)");

            table.HasCheckConstraint(
                "CK_Hunter_Submissions_Latitude",
                "[Latitude] BETWEEN -90 AND 90");

            table.HasCheckConstraint(
                "CK_Hunter_Submissions_Longitude",
                "[Longitude] BETWEEN -180 AND 180");

            table.HasCheckConstraint(
                "CK_Hunter_Submissions_GpsAccuracy",
                "[GpsAccuracyMeters] IS NULL OR [GpsAccuracyMeters] > 0");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.Property(x => x.UserId)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(x => x.UserNameSnapshot)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.PlaceName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasMaxLength(2000);

        builder.Property(x => x.District)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Latitude)
            .IsRequired();

        builder.Property(x => x.Longitude)
            .IsRequired();

        builder.Property(x => x.GpsAccuracyMeters);

        builder.Property(x => x.LocationCapturedAtUtc)
            .HasColumnType("datetimeoffset");

        builder.Property(x => x.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnType("datetimeoffset")
            .IsRequired();

        builder.Property(x => x.SubmittedAtUtc)
            .HasColumnType("datetimeoffset");

        // M1 user account relationship.
        // No automatic deletion of Hunter submissions.
        builder.HasOne<UserAccount>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName(
                "FK_Hunter_Submissions_Users_UserId");

        builder.HasIndex(x => x.UserId)
            .HasDatabaseName(
                "IX_Hunter_Submissions_UserId");

        builder.HasIndex(x => new
        {
            x.District,
            x.Status
        })
        .HasDatabaseName(
            "IX_Hunter_Submissions_District_Status");

        builder.HasAlternateKey(x => new { x.Id, x.UserId })
            .HasName("AK_Hunter_Submissions_Id_UserId");
    }
}
