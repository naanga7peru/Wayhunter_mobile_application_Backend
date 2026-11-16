
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wayhunter.Domain.Modules.HunterPlaces.Entities;

namespace Wayhunter.Infrastructure.Persistence.Configurations.HunterPlaces;

public sealed class HunterReviewDecisionConfiguration
    : IEntityTypeConfiguration<HunterReviewDecision>
{
    public void Configure(
        EntityTypeBuilder<HunterReviewDecision> builder)
    {
        builder.ToTable("Hunter_ReviewDecisions", table =>
        {
            table.HasCheckConstraint(
                "CK_Hunter_ReviewDecisions_Status",
                "[ResultingStatus] IN (2, 3, 4)");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.Property(x => x.HunterSubmissionId)
            .IsRequired();

        builder.Property(x => x.ReviewerUserId)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(x => x.ResultingStatus)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.Reason)
            .HasMaxLength(2000);

        builder.Property(x => x.ReviewedAtUtc)
            .HasColumnType("datetimeoffset")
            .IsRequired();

        builder.HasOne<HunterSubmission>()
            .WithMany()
            .HasForeignKey(x => x.HunterSubmissionId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName(
                "FK_Hunter_ReviewDecisions_Submissions");

        builder.HasIndex(x => new
        {
            x.HunterSubmissionId,
            x.ReviewedAtUtc
        })
        .HasDatabaseName(
            "IX_Hunter_ReviewDecisions_Submission_Date");
    }
}
