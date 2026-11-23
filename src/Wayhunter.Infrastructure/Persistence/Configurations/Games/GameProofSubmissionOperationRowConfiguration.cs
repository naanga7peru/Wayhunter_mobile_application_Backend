using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wayhunter.Infrastructure.Persistence.Models.Games;

namespace Wayhunter.Infrastructure.Persistence.Configurations.Games;

/// <summary>
/// Proposed M5 recovery schema. DB Coordinator sign-off required.
/// Not registered for production or included in migrations.
/// </summary>
public sealed class GameProofSubmissionOperationRowConfiguration
    : IEntityTypeConfiguration<GameProofSubmissionOperationRow>
{
    public void Configure(EntityTypeBuilder<GameProofSubmissionOperationRow> b)
    {
        b.ToTable("GameProofSubmissionOperations", t =>
        {
            t.HasCheckConstraint("CK_GameProofOperations_State", "[State] IN (0,1,2,3,4,5,6)");
            t.HasCheckConstraint("CK_GameProofOperations_Step", "[StepNumber] >= 1");
            t.HasCheckConstraint("CK_GameProofOperations_RecoveryAttempts", "[RecoveryAttemptCount] >= 0");
        });
        b.HasKey(x => x.SubmissionOperationId);
        b.Property(x => x.SubmissionOperationId).ValueGeneratedNever();
        b.Property(x => x.AuthenticatedPlayerUserId).HasMaxLength(256).IsRequired();
        b.Property(x => x.CaptureSessionId).HasMaxLength(256).IsRequired();
        b.Property(x => x.MediaProviderNamespace).HasMaxLength(64).IsRequired();
        b.Property(x => x.CanonicalMediaId).HasMaxLength(256).IsRequired();
        b.Property(x => x.State).HasConversion<int>().IsRequired();
        b.Property(x => x.ClaimToken).HasMaxLength(128);
        b.Property(x => x.ProviderReceiptId).HasMaxLength(256);
        b.Property(x => x.LastErrorCode).HasMaxLength(128);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasOne<GameAttemptRow>()
            .WithMany()
            .HasForeignKey(x => new { x.AttemptId, x.GameId, x.AuthenticatedPlayerUserId })
            .HasPrincipalKey(x => new { x.Id, x.GameId, x.PlayerUserId })
            .OnDelete(DeleteBehavior.NoAction);

        b.HasIndex(x => new { x.MediaProviderNamespace, x.CanonicalMediaId })
            .IsUnique()
            .HasDatabaseName("UX_GameProofOperations_Provider_Media");

        b.HasIndex(x => new { x.MediaProviderNamespace, x.CaptureSessionId })
            .IsUnique()
            .HasDatabaseName("UX_GameProofOperations_Provider_Capture");

        b.HasIndex(x => new { x.State, x.NextReconciliationAtUtc })
            .HasDatabaseName("IX_GameProofOperations_Reconciliation");
    }
}
