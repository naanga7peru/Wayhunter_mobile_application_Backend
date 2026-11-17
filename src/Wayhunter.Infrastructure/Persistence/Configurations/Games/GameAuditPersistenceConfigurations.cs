
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Wayhunter.Infrastructure.Persistence.Models.Games;

namespace Wayhunter.Infrastructure.Persistence.Configurations.Games;

public sealed class GameCreationApprovalRowConfiguration
    : IEntityTypeConfiguration<GameCreationApprovalRow>
{
    public void Configure(
        EntityTypeBuilder<GameCreationApprovalRow> b)
    {
        b.ToTable("GameCreationApprovals");

        b.HasKey(x => x.ApprovalRecordId);

        b.Property(x => x.ApprovalRecordId)
            .HasMaxLength(256)
            .ValueGeneratedNever()
            .IsRequired();

        b.Property(x => x.AdminUserId)
            .HasMaxLength(256)
            .IsRequired();

        b.Property(x => x.ApprovedAtUtc)
            .HasColumnType("datetimeoffset")
            .IsRequired();

        b.Property(x => x.RewardRuleVersion)
            .HasMaxLength(100)
            .IsRequired();

        b.HasOne<GameRow>()
            .WithMany()
            .HasForeignKey(x => x.GameId)
            .OnDelete(DeleteBehavior.NoAction);

        b.HasOne<UserAccount>()
            .WithMany()
            .HasForeignKey(x => x.AdminUserId)
            .OnDelete(DeleteBehavior.NoAction);

        // Exactly one creation approval per game.
        b.HasIndex(x => x.GameId)
            .IsUnique()
            .HasDatabaseName(
                "UX_GameCreationApprovals_Game");

        // A reward event cannot be assigned twice.
        b.HasIndex(x => x.RewardEventId)
            .IsUnique()
            .HasDatabaseName(
                "UX_GameCreationApprovals_RewardEvent");

        b.HasIndex(x => x.CorrelationId)
            .IsUnique()
            .HasDatabaseName(
                "UX_GameCreationApprovals_Correlation");
    }
}

public sealed class GameModerationDecisionRowConfiguration
    : IEntityTypeConfiguration<GameModerationDecisionRow>
{
    public void Configure(
        EntityTypeBuilder<GameModerationDecisionRow> b)
    {
        b.ToTable("GameModerationDecisions", table =>
        {
            table.HasCheckConstraint(
                "CK_GameModeration_Action",
                "[Action] IN (1,2,3)");

            table.HasCheckConstraint(
                "CK_GameModeration_Status",
                "[ResultingStatus] IN (0,1,2,3,4,5)");
        });

        b.HasKey(x => x.DecisionId);

        b.Property(x => x.DecisionId)
            .ValueGeneratedNever();

        b.Property(x => x.AdminUserId)
            .HasMaxLength(256)
            .IsRequired();

        b.Property(x => x.Action)
            .HasConversion<int>()
            .IsRequired();

        b.Property(x => x.ResultingStatus)
            .HasConversion<int>()
            .IsRequired();

        b.Property(x => x.Reason)
            .HasMaxLength(1000)
            .IsRequired();

        b.Property(x => x.OccurredAtUtc)
            .HasColumnType("datetimeoffset")
            .IsRequired();

        b.HasOne<GameRow>()
            .WithMany()
            .HasForeignKey(x => x.GameId)
            .OnDelete(DeleteBehavior.NoAction);

        b.HasOne<UserAccount>()
            .WithMany()
            .HasForeignKey(x => x.AdminUserId)
            .OnDelete(DeleteBehavior.NoAction);

        b.HasIndex(x => new
        {
            x.GameId,
            x.OccurredAtUtc
        })
        .HasDatabaseName(
            "IX_GameModeration_Game_Time");

        b.HasIndex(x => x.CorrelationId)
            .IsUnique()
            .HasDatabaseName(
                "UX_GameModeration_Correlation");
    }
}

public sealed class GameProofReviewDecisionRowConfiguration
    : IEntityTypeConfiguration<GameProofReviewDecisionRow>
{
    public void Configure(
        EntityTypeBuilder<GameProofReviewDecisionRow> b)
    {
        b.ToTable("GameProofReviewDecisions");

        b.HasKey(x => x.DecisionId);

        b.Property(x => x.DecisionId)
            .ValueGeneratedNever();

        b.Property(x => x.ReviewerUserId)
            .HasMaxLength(256)
            .IsRequired();

        b.Property(x => x.Reason)
            .HasMaxLength(1000);

        b.Property(x => x.ReviewedAtUtc)
            .HasColumnType("datetimeoffset")
            .IsRequired();

        b.HasOne<GameProofEvidenceRow>()
            .WithMany()
            .HasForeignKey(x => x.EvidenceId)
            .OnDelete(DeleteBehavior.NoAction);

        b.HasOne<UserAccount>()
            .WithMany()
            .HasForeignKey(x => x.ReviewerUserId)
            .OnDelete(DeleteBehavior.NoAction);

        // One decision for each evidence submission.
        // Another retake has a different EvidenceId.
        b.HasIndex(x => x.EvidenceId)
            .IsUnique()
            .HasDatabaseName(
                "UX_GameProofReviews_Evidence");

        b.HasIndex(x => x.CorrelationId)
            .IsUnique()
            .HasDatabaseName(
                "UX_GameProofReviews_Correlation");
    }
}

public sealed class GameResultDecisionRowConfiguration
    : IEntityTypeConfiguration<GameResultDecisionRow>
{
    public void Configure(
        EntityTypeBuilder<GameResultDecisionRow> b)
    {
        b.ToTable("GameResultDecisions");

        b.HasKey(x => x.DecisionId);

        b.Property(x => x.DecisionId)
            .ValueGeneratedNever();

        b.Property(x => x.AdminUserId)
            .HasMaxLength(256)
            .IsRequired();

        b.Property(x => x.Reason)
            .HasMaxLength(1000);

        b.Property(x => x.OccurredAtUtc)
            .HasColumnType("datetimeoffset")
            .IsRequired();

        b.HasOne<GameRow>()
            .WithMany()
            .HasForeignKey(x => x.GameId)
            .OnDelete(DeleteBehavior.NoAction);

        b.HasOne<GameAttemptRow>()
            .WithMany()
            .HasForeignKey(x => new { x.AttemptId, x.GameId })
            .HasPrincipalKey(x => new { x.Id, x.GameId })
            .OnDelete(DeleteBehavior.NoAction);

        b.HasOne<UserAccount>()
            .WithMany()
            .HasForeignKey(x => x.AdminUserId)
            .OnDelete(DeleteBehavior.NoAction);

        // Many rejected attempts are allowed.
        b.HasIndex(x => new
        {
            x.GameId,
            x.OccurredAtUtc
        })
        .HasDatabaseName(
            "IX_GameResultDecisions_Game_Time");

        // At most one approved result per Game.
        // The GameWinners table also uses GameId as PK.
        b.HasIndex(x => x.GameId)
            .IsUnique()
            .HasFilter("[IsApproved] = 1")
            .HasDatabaseName(
                "UX_GameResults_OneApprovedPerGame");

        b.HasIndex(x => x.CorrelationId)
            .IsUnique()
            .HasDatabaseName(
                "UX_GameResults_Correlation");
    }
}
