
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Wayhunter.Infrastructure.Persistence.Models.Games;

namespace Wayhunter.Infrastructure.Persistence.Configurations.Games;

public sealed class GameRowConfiguration
    : IEntityTypeConfiguration<GameRow>
{
    public void Configure(EntityTypeBuilder<GameRow> b)
    {
        b.ToTable("Games", t =>
        {
            t.HasCheckConstraint(
                "CK_Games_Status",
                "[Status] IN (0,1,2,3,4,5)");

            t.HasCheckConstraint(
                "CK_Games_Duration",
                "[DurationMinutes] IS NULL OR [DurationMinutes] > 0");

            t.HasCheckConstraint(
                "CK_Games_ExpiryOrder",
                "([ApprovedAtUtc] IS NULL AND [ExpiresAtUtc] IS NULL) OR " +
                "([ApprovedAtUtc] IS NOT NULL AND " +
                "[ExpiresAtUtc] IS NOT NULL AND " +
                "[ExpiresAtUtc] = DATEADD(hour, 720, [ApprovedAtUtc]))");
        });

        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();

        b.Property(x => x.CreatorUserId)
            .HasMaxLength(256).IsRequired();

        b.Property(x => x.Title).HasMaxLength(150);
        b.Property(x => x.Description).HasMaxLength(2000);
        b.Property(x => x.FlagReason).HasMaxLength(1000);
        b.Property(x => x.RejectionReason).HasMaxLength(1000);
        b.Property(x => x.DisabledReason).HasMaxLength(1000);

        b.Property(x => x.Status)
            .HasConversion<int>().IsRequired();

        b.Property(x => x.SubmittedAtUtc)
            .HasColumnType("datetimeoffset");

        b.Property(x => x.ApprovedAtUtc)
            .HasColumnType("datetimeoffset");

        b.Property(x => x.ExpiresAtUtc)
            .HasColumnType("datetimeoffset");

        b.Property(x => x.LastModeratedAtUtc)
            .HasColumnType("datetimeoffset");

        b.Property(x => x.RowVersion).IsRowVersion();

        b.HasOne<UserAccount>()
            .WithMany()
            .HasForeignKey(x => x.CreatorUserId)
            .OnDelete(DeleteBehavior.NoAction);

        b.HasIndex(x => new
        {
            x.Status,
            x.ExpiresAtUtc
        }).HasDatabaseName("IX_Games_Status_Expiry");

        b.HasIndex(x => new
        {
            x.CreatorUserId,
            x.SubmittedAtUtc
        }).HasDatabaseName("IX_Games_Creator_Submitted");
    }
}

public sealed class GameStepRowConfiguration
    : IEntityTypeConfiguration<GameStepRow>
{
    public void Configure(EntityTypeBuilder<GameStepRow> b)
    {
        b.ToTable("GameSteps", t =>
        {
            t.HasCheckConstraint(
                "CK_GameSteps_Number",
                "[StepNumber] >= 1");
        });

        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.HasAlternateKey(x => new { x.GameId, x.StepNumber })
            .HasName("AK_GameSteps_Game_StepNumber");

        b.HasOne<GameRow>()
            .WithMany()
            .HasForeignKey(x => x.GameId)
            .OnDelete(DeleteBehavior.NoAction);

        b.HasIndex(x => new
        {
            x.GameId,
            x.StepNumber
        }).IsUnique()
        .HasDatabaseName("UX_GameSteps_Game_Number");
    }
}

public sealed class GameHintRowConfiguration
    : IEntityTypeConfiguration<GameHintRow>
{
    public void Configure(EntityTypeBuilder<GameHintRow> b)
    {
        b.ToTable("GameHints", t =>
        {
            t.HasCheckConstraint(
                "CK_GameHints_Content",
                "[TextHint] IS NOT NULL OR [PhotoMediaId] IS NOT NULL");
        });

        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();

        b.Property(x => x.TextHint).HasMaxLength(2000);
        b.Property(x => x.PhotoMediaId).HasMaxLength(256);

        b.HasOne<GameStepRow>()
            .WithMany()
            .HasForeignKey(x => x.GameStepId)
            .OnDelete(DeleteBehavior.NoAction);

        b.HasIndex(x => x.GameStepId);
    }
}

public sealed class GameAttemptRowConfiguration
    : IEntityTypeConfiguration<GameAttemptRow>
{
    public void Configure(EntityTypeBuilder<GameAttemptRow> b)
    {
        b.ToTable("GameAttempts", t =>
        {
            t.HasCheckConstraint(
                "CK_GameAttempts_Deadline",
                "[DeadlineUtc] IS NULL OR " +
                "[DeadlineUtc] > [StartedAtUtc]");
        });

        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.HasAlternateKey(x => new { x.Id, x.GameId })
            .HasName("AK_GameAttempts_Id_Game");
        b.HasAlternateKey(x => new
        {
            x.Id,
            x.GameId,
            x.PlayerUserId
        }).HasName("AK_GameAttempts_Id_Game_Player");

        b.Property(x => x.PlayerUserId)
            .HasMaxLength(256).IsRequired();

        b.Property(x => x.StartedAtUtc)
            .HasColumnType("datetimeoffset");

        b.Property(x => x.DeadlineUtc)
            .HasColumnType("datetimeoffset");

        b.Property(x => x.RowVersion).IsRowVersion();

        b.HasOne<GameRow>()
            .WithMany()
            .HasForeignKey(x => x.GameId)
            .OnDelete(DeleteBehavior.NoAction);

        b.HasOne<UserAccount>()
            .WithMany()
            .HasForeignKey(x => x.PlayerUserId)
            .OnDelete(DeleteBehavior.NoAction);

        b.HasIndex(x => new
        {
            x.GameId,
            x.PlayerUserId
        }).HasDatabaseName("IX_GameAttempts_Game_Player");
    }
}

public sealed class GameAttemptProgressRowConfiguration
    : IEntityTypeConfiguration<GameAttemptProgressRow>
{
    public void Configure(EntityTypeBuilder<GameAttemptProgressRow> b)
    {
        b.ToTable("GameAttemptProgress", t =>
        {
            t.HasCheckConstraint(
                "CK_GameProgress_Steps",
                "[TotalSteps] > 0 AND " +
                "[CurrentStepNumber] >= 1 AND " +
                "[CurrentStepNumber] <= [TotalSteps]");

            t.HasCheckConstraint(
                "CK_GameProgress_Rejections",
                "[RejectedEvidenceCount] BETWEEN 0 AND 3");

            t.HasCheckConstraint(
                "CK_GameProgress_Status",
                "[Status] IN (0,1,2,3,4)");
        });

        b.HasKey(x => x.AttemptId);

        b.Property(x => x.AttemptId).ValueGeneratedNever();

        b.Property(x => x.PlayerUserId)
            .HasMaxLength(256).IsRequired();

        b.Property(x => x.Status)
            .HasConversion<int>().IsRequired();

        b.HasOne<GameAttemptRow>()
            .WithMany()
            .HasForeignKey(
                x => new { x.AttemptId, x.GameId, x.PlayerUserId })
            .HasPrincipalKey(x => new
            {
                x.Id,
                x.GameId,
                x.PlayerUserId
            })
            .OnDelete(DeleteBehavior.NoAction);

        b.HasIndex(x => new { x.GameId, x.PlayerUserId })
            .IsUnique()
            .HasFilter("[Status] IN (0,1,2)")
            .HasDatabaseName("UX_GameProgress_Active_Game_Player");
    }
}

public sealed class GameProofEvidenceRowConfiguration
    : IEntityTypeConfiguration<GameProofEvidenceRow>
{
    public void Configure(EntityTypeBuilder<GameProofEvidenceRow> b)
    {
        b.ToTable("GameProofEvidence", t =>
        {
            t.HasCheckConstraint(
                "CK_GameProofEvidence_Step",
                "[StepNumber] >= 1");
        });

        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();

        b.Property(x => x.MediaId)
            .HasMaxLength(256).IsRequired();

        b.Property(x => x.SubmittedAtUtc)
            .HasColumnType("datetimeoffset");

        b.HasOne<GameAttemptRow>()
            .WithMany()
            .HasForeignKey(x => new { x.AttemptId, x.GameId })
            .HasPrincipalKey(x => new { x.Id, x.GameId })
            .OnDelete(DeleteBehavior.NoAction);

        b.HasOne<GameStepRow>()
            .WithMany()
            .HasForeignKey(x => new { x.GameId, x.StepNumber })
            .HasPrincipalKey(x => new { x.GameId, x.StepNumber })
            .OnDelete(DeleteBehavior.NoAction);

        b.HasAlternateKey(x => new { x.Id, x.AttemptId })
            .HasName("AK_GameProofEvidence_Id_Attempt");

        // Multiple retakes per attempt-step are allowed.
        b.HasIndex(x => new
        {
            x.AttemptId,
            x.StepNumber,
            x.SubmittedAtUtc
        }).HasDatabaseName(
            "IX_GameProofEvidence_Attempt_Step_Time");
    }
}

public sealed class GameWinnerRowConfiguration
    : IEntityTypeConfiguration<GameWinnerRow>
{
    public void Configure(EntityTypeBuilder<GameWinnerRow> b)
    {
        b.ToTable("GameWinners");

        // One GameId can appear only once.
        b.HasKey(x => x.GameId);
        b.Property(x => x.GameId).ValueGeneratedNever();

        b.Property(x => x.PlayerUserId)
            .HasMaxLength(256).IsRequired();

        b.Property(x => x.ApprovedByUserId)
            .HasMaxLength(256).IsRequired();

        b.Property(x => x.ApprovedAtUtc)
            .HasColumnType("datetimeoffset");

        b.HasOne<GameRow>()
            .WithOne()
            .HasForeignKey<GameWinnerRow>(x => x.GameId)
            .OnDelete(DeleteBehavior.NoAction);

        b.HasOne<GameAttemptRow>()
            .WithMany()
            .HasForeignKey(x => new
            {
                x.AttemptId,
                x.GameId,
                x.PlayerUserId
            })
            .HasPrincipalKey(x => new
            {
                x.Id,
                x.GameId,
                x.PlayerUserId
            })
            .OnDelete(DeleteBehavior.NoAction);

        b.HasOne<UserAccount>()
            .WithMany()
            .HasForeignKey(x => x.PlayerUserId)
            .OnDelete(DeleteBehavior.NoAction);

        b.HasOne<UserAccount>()
            .WithMany()
            .HasForeignKey(x => x.ApprovedByUserId)
            .OnDelete(DeleteBehavior.NoAction);

        b.HasIndex(x => x.AttemptId)
            .IsUnique()
            .HasDatabaseName("UX_GameWinners_Attempt");
    }
}

public sealed class GameCompletionResultRowConfiguration
    : IEntityTypeConfiguration<GameCompletionResultRow>
{
    public void Configure(EntityTypeBuilder<GameCompletionResultRow> b)
    {
        b.ToTable("GameCompletionResults", table =>
        {
            table.HasCheckConstraint(
                "CK_GameCompletionResults_Status",
                "[Status] IN (0,1,2)");

            table.HasCheckConstraint(
                "CK_GameCompletionResults_Timeline",
                "[AttemptDeadlineUtc] > [AttemptStartedAtUtc] AND " +
                "[CompletedAtUtc] >= [AttemptStartedAtUtc] AND " +
                "[CompletedAtUtc] < [AttemptDeadlineUtc]");
        });

        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();

        b.Property(x => x.PlayerUserId)
            .HasMaxLength(256)
            .IsRequired();

        b.Property(x => x.AttemptStartedAtUtc)
            .HasColumnType("datetimeoffset")
            .IsRequired();

        b.Property(x => x.AttemptDeadlineUtc)
            .HasColumnType("datetimeoffset")
            .IsRequired();

        b.Property(x => x.CompletedAtUtc)
            .HasColumnType("datetimeoffset")
            .IsRequired();

        b.Property(x => x.Status)
            .HasConversion<int>()
            .IsRequired();

        b.Property(x => x.RowVersion).IsRowVersion();

        b.HasOne<GameRow>()
            .WithMany()
            .HasForeignKey(x => x.GameId)
            .OnDelete(DeleteBehavior.NoAction);

        b.HasOne<GameAttemptRow>()
            .WithMany()
            .HasForeignKey(x => new
            {
                x.AttemptId,
                x.GameId,
                x.PlayerUserId
            })
            .HasPrincipalKey(x => new
            {
                x.Id,
                x.GameId,
                x.PlayerUserId
            })
            .OnDelete(DeleteBehavior.NoAction);

        b.HasOne<UserAccount>()
            .WithMany()
            .HasForeignKey(x => x.PlayerUserId)
            .OnDelete(DeleteBehavior.NoAction);

        b.HasIndex(x => x.AttemptId)
            .IsUnique()
            .HasDatabaseName("UX_GameCompletionResults_Attempt");

        b.HasIndex(x => new
        {
            x.GameId,
            x.Status,
            x.CompletedAtUtc
        }).HasDatabaseName("IX_GameCompletionResults_Game_Status_Time");
    }
}

public sealed class GameCompletionEvidenceRowConfiguration
    : IEntityTypeConfiguration<GameCompletionEvidenceRow>
{
    public void Configure(EntityTypeBuilder<GameCompletionEvidenceRow> b)
    {
        b.ToTable("GameCompletionEvidence");

        b.HasKey(x => new
        {
            x.CompletionResultId,
            x.EvidenceId
        });

        b.HasOne<GameCompletionResultRow>()
            .WithMany()
            .HasForeignKey(x => new
            {
                x.CompletionResultId,
                x.AttemptId
            })
            .HasPrincipalKey(x => new { x.Id, x.AttemptId })
            .OnDelete(DeleteBehavior.NoAction);

        b.HasOne<GameProofEvidenceRow>()
            .WithMany()
            .HasForeignKey(x => new { x.EvidenceId, x.AttemptId })
            .HasPrincipalKey(x => new { x.Id, x.AttemptId })
            .OnDelete(DeleteBehavior.NoAction);

        b.HasIndex(x => x.EvidenceId)
            .IsUnique()
            .HasDatabaseName("UX_GameCompletionEvidence_Evidence");
    }
}
