
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wayhunter.Domain.Modules.HunterPlaces.Entities;
using Wayhunter.Infrastructure.Persistence.Models.HunterPlaces;

namespace Wayhunter.Infrastructure.Persistence.Configurations.HunterPlaces;

public sealed class HunterCaptureSessionRowConfiguration
    : IEntityTypeConfiguration<HunterCaptureSessionRow>
{
    public void Configure(
        EntityTypeBuilder<HunterCaptureSessionRow> builder)
    {
        builder.ToTable("Hunter_CaptureSessions", table =>
        {
            table.HasCheckConstraint(
                "CK_Hunter_CaptureSessions_TimeWindow",
                "[ExpiresAtUtc] > [IssuedAtUtc]");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.Property(x => x.HunterId)
            .HasColumnType("uniqueidentifier")
            .IsRequired();

        builder.Property(x => x.HunterSubmissionId)
            .HasColumnType("uniqueidentifier")
            .IsRequired();

        builder.Property(x => x.IssuedAtUtc)
            .HasColumnType("datetimeoffset")
            .IsRequired();

        builder.Property(x => x.ExpiresAtUtc)
            .HasColumnType("datetimeoffset")
            .IsRequired();

        builder.Property(x => x.IsConsumed)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(x => x.IsRevoked)
            .HasDefaultValue(false)
            .IsRequired();

        // Every session belongs to a Hunter submission.
        builder.HasOne<HunterSubmission>()
            .WithMany()
            .HasForeignKey(x => x.HunterSubmissionId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName(
                "FK_Hunter_CaptureSessions_Submissions");

        builder.HasIndex(x => new
        {
            x.HunterSubmissionId,
            x.IsRevoked,
            x.IsConsumed
        })
        .HasDatabaseName(
            "IX_Hunter_CaptureSessions_Submission_State");

        builder.HasIndex(x => new
        {
            x.HunterId,
            x.IsRevoked,
            x.IsConsumed
        })
        .HasDatabaseName(
            "IX_Hunter_CaptureSessions_Hunter_State");
    }
}
