
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
            table.HasCheckConstraint(
                "CK_Hunter_Evidence_VerifiedBinding",
                "[ProviderVerifiedAtUtc] IS NULL OR " +
                "([CaptureSessionId] IS NOT NULL AND [OwnerUserId] IS NOT NULL " +
                "AND [ProviderAssetIdHash] IS NOT NULL AND [ProviderReceiptDigest] IS NOT NULL)");
            table.HasCheckConstraint(
                "CK_Hunter_Evidence_ProviderHashLengths",
                "([ProviderAssetIdHash] IS NULL OR LEN([ProviderAssetIdHash]) = 64) AND " +
                "([ProviderReceiptDigest] IS NULL OR LEN([ProviderReceiptDigest]) = 64)");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.Property(x => x.HunterSubmissionId)
            .IsRequired();
        builder.Property(x => x.CaptureSessionId);
        builder.Property(x => x.OwnerUserId).HasMaxLength(256);
        builder.Property(x => x.ProviderAssetIdHash).HasMaxLength(64);
        builder.Property(x => x.ProviderReceiptDigest).HasMaxLength(64);
        builder.Property(x => x.ProviderVerifiedAtUtc).HasColumnType("datetimeoffset");

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

        builder.HasOne<HunterSubmission>()
            .WithMany()
            .HasForeignKey(x => new { x.HunterSubmissionId, x.OwnerUserId })
            .HasPrincipalKey(x => new { x.Id, x.UserId })
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_Hunter_Evidence_Submission_Owner");

        builder.HasOne<Wayhunter.Infrastructure.Persistence.Models.HunterPlaces.HunterCaptureSessionRow>()
            .WithMany()
            .HasForeignKey(x => new { x.CaptureSessionId, x.HunterSubmissionId })
            .HasPrincipalKey(x => new { x.Id, x.HunterSubmissionId })
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_Hunter_Evidence_CaptureSession_Owner");

        builder.HasIndex(x => new
        {
            x.HunterSubmissionId,
            x.EvidenceType
        })
        .HasDatabaseName(
            "IX_Hunter_Evidence_Submission_Type");

        builder.HasIndex(x => new { x.HunterSubmissionId, x.EvidenceType }, "VerifiedEvidencePerSubmissionType")
            .IsUnique()
            .HasFilter("[ProviderVerifiedAtUtc] IS NOT NULL")
            .HasDatabaseName("UX_Hunter_Evidence_Verified_Submission_Type");
        builder.HasIndex(x => x.CaptureSessionId)
            .IsUnique()
            .HasFilter("[CaptureSessionId] IS NOT NULL")
            .HasDatabaseName("UX_Hunter_Evidence_CaptureSession");
        builder.HasIndex(x => x.ProviderAssetIdHash)
            .IsUnique()
            .HasFilter("[ProviderAssetIdHash] IS NOT NULL")
            .HasDatabaseName("UX_Hunter_Evidence_ProviderAsset");
    }
}
