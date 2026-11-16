
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wayhunter.Domain.Modules.HunterPlaces.Entities;

namespace Wayhunter.Infrastructure.Persistence.Configurations.HunterPlaces;

public sealed class HunterEvidenceConfiguration
    : IEntityTypeConfiguration<HunterEvidence>
{
    public void Configure(
        EntityTypeBuilder<HunterEvidence> builder)
    {
        builder.ToTable("Hunter_Evidence", table =>
        {
            table.HasCheckConstraint(
                "CK_Hunter_Evidence_Type",
                "[EvidenceType] IN (1, 2)");

            table.HasCheckConstraint(
                "CK_Hunter_Evidence_CoordinatePair",
                "([CaptureLatitude] IS NULL AND [CaptureLongitude] IS NULL) OR " +
                "([CaptureLatitude] IS NOT NULL AND [CaptureLongitude] IS NOT NULL)");

            table.HasCheckConstraint(
                "CK_Hunter_Evidence_Duration",
                "[DurationSeconds] IS NULL OR [DurationSeconds] > 0");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.Property(x => x.HunterSubmissionId)
            .IsRequired();

        builder.Property(x => x.EvidenceType)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.PrivateMediaReference)
            .HasMaxLength(2048)
            .IsRequired();

        builder.Property(x => x.CapturedAtUtc)
            .HasColumnType("datetimeoffset")
            .IsRequired();

        builder.Property(x => x.UploadedAtUtc)
            .HasColumnType("datetimeoffset")
            .IsRequired();

        builder.Property(x => x.CaptureLatitude);
        builder.Property(x => x.CaptureLongitude);
        builder.Property(x => x.DurationSeconds);

        builder.HasOne<HunterSubmission>()
            .WithMany()
            .HasForeignKey(x => x.HunterSubmissionId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName(
                "FK_Hunter_Evidence_Submissions");

        builder.HasIndex(x => new
        {
            x.HunterSubmissionId,
            x.EvidenceType
        })
        .HasDatabaseName(
            "IX_Hunter_Evidence_Submission_Type");
    }
}
