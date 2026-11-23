using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wayhunter.Infrastructure.Persistence.Models.Games;

namespace Wayhunter.Infrastructure.Persistence.Configurations.Games;

/// <summary>
/// Design v3 EF metadata PROPOSAL ONLY. DO NOT register via ApplyConfigurationsFromAssembly,
/// WayhunterSharedDbContext, shared migrations, or production DI. The long
/// seven-column principal key must pass DB Coordinator index-width review.
/// SQL guards/permissions and trusted M4 provenance are NOT enforced by EF.
/// </summary>
public sealed class GameProofMediaClaimRowConfiguration
    : IEntityTypeConfiguration<GameProofMediaClaimRow>
{
    public void Configure(EntityTypeBuilder<GameProofMediaClaimRow> b)
    {
        b.ToTable("GameProofMediaClaims", t =>
        {
            t.HasCheckConstraint("CK_GameProofMediaClaims_IdsAndStep",
                "[MediaClaimId] <> '00000000-0000-0000-0000-000000000000' AND " +
                "[GameId] <> '00000000-0000-0000-0000-000000000000' AND " +
                "[AttemptId] <> '00000000-0000-0000-0000-000000000000' AND [StepNumber] >= 1 AND " +
                "([SubmissionOperationId] IS NULL OR [SubmissionOperationId] <> '00000000-0000-0000-0000-000000000000') AND " +
                "([EvidenceId] IS NULL OR [EvidenceId] <> '00000000-0000-0000-0000-000000000000')");
            t.HasCheckConstraint("CK_GameProofMediaClaims_IdentityNonblank",
                "DATALENGTH([MediaProviderNamespace]) > 0 AND DATALENGTH([CanonicalMediaId]) > 0 AND " +
                "DATALENGTH([AuthenticatedPlayerUserId]) > 0 AND " +
                "LEN(LTRIM(RTRIM([MediaProviderNamespace]))) > 0 AND " +
                "LEN(LTRIM(RTRIM([CanonicalMediaId]))) > 0 AND " +
                "LEN(LTRIM(RTRIM([AuthenticatedPlayerUserId]))) > 0");
            t.HasCheckConstraint("CK_GameProofMediaClaims_OriginState",
                "[ClaimOrigin] IN (1,2) AND [ClaimState] IN (0,1,2,3,4) AND " +
                "(([ClaimOrigin] = 1 AND [SubmissionOperationId] IS NOT NULL AND [ClaimState] IN (0,1,2,3)) OR " +
                "([ClaimOrigin] = 2 AND [SubmissionOperationId] IS NULL AND [ClaimState] = 4 AND [EvidenceId] IS NOT NULL))");
            t.HasCheckConstraint("CK_GameProofMediaClaims_StateFields",
                "(([ClaimState] = 0 AND [EvidenceId] IS NULL AND [ProviderConsumedAtUtc] IS NULL AND [EvidenceCommittedAtUtc] IS NULL) OR " +
                "([ClaimState] = 1 AND [EvidenceId] IS NULL AND [ProviderConsumedAtUtc] IS NOT NULL AND [EvidenceCommittedAtUtc] IS NULL) OR " +
                "([ClaimState] = 2 AND [EvidenceId] IS NOT NULL AND [ProviderConsumedAtUtc] IS NOT NULL AND [EvidenceCommittedAtUtc] IS NOT NULL) OR " +
                "([ClaimState] = 3 AND [EvidenceId] IS NULL AND [ProviderConsumedAtUtc] IS NULL AND [EvidenceCommittedAtUtc] IS NULL) OR " +
                "([ClaimState] = 4 AND [EvidenceId] IS NOT NULL AND [ProviderConsumedAtUtc] IS NULL AND [EvidenceCommittedAtUtc] IS NULL))");
            t.HasCheckConstraint("CK_GameProofMediaClaims_TimeOrder",
                "[UpdatedAtUtc] >= [CreatedAtUtc] AND " +
                "([ProviderConsumedAtUtc] IS NULL OR [ProviderConsumedAtUtc] >= [CreatedAtUtc]) AND " +
                "([EvidenceCommittedAtUtc] IS NULL OR " +
                "([ProviderConsumedAtUtc] IS NOT NULL AND [EvidenceCommittedAtUtc] >= [ProviderConsumedAtUtc])) AND " +
                "([EvidenceCommittedAtUtc] IS NULL OR [UpdatedAtUtc] >= [EvidenceCommittedAtUtc])");
        });

        b.HasKey(x => x.MediaClaimId).HasName("PK_GameProofMediaClaims");
        b.Property(x => x.MediaClaimId).ValueGeneratedNever();
        b.Property(x => x.MediaProviderNamespace).HasMaxLength(64).UseCollation("Latin1_General_100_BIN2").IsRequired();
        b.Property(x => x.CanonicalMediaId).HasMaxLength(256).UseCollation("Latin1_General_100_BIN2").IsRequired();
        b.Property(x => x.AuthenticatedPlayerUserId).HasMaxLength(256).IsRequired();
        b.Property(x => x.ClaimOrigin).HasConversion<byte>().IsRequired();
        b.Property(x => x.ClaimState).HasConversion<byte>().IsRequired();
        b.Property(x => x.ProviderConsumedAtUtc).HasColumnType("datetimeoffset(7)");
        b.Property(x => x.EvidenceCommittedAtUtc).HasColumnType("datetimeoffset(7)");
        b.Property(x => x.CreatedAtUtc).HasColumnType("datetimeoffset(7)").IsRequired();
        b.Property(x => x.UpdatedAtUtc).HasColumnType("datetimeoffset(7)").IsRequired();
        b.Property(x => x.RowVersion).IsRowVersion();

        b.HasIndex(x => new { x.MediaProviderNamespace, x.CanonicalMediaId })
            .IsUnique().HasDatabaseName("UX_GameProofMediaClaims_Provider_Canonical");
        b.HasIndex(x => x.SubmissionOperationId).IsUnique()
            .HasFilter("[SubmissionOperationId] IS NOT NULL")
            .HasDatabaseName("UX_GameProofMediaClaims_Operation");
        b.HasIndex(x => x.EvidenceId).IsUnique()
            .HasFilter("[EvidenceId] IS NOT NULL")
            .HasDatabaseName("UX_GameProofMediaClaims_Evidence");
        b.HasIndex(x => new { x.AttemptId, x.StepNumber, x.ClaimState })
            .HasDatabaseName("IX_GameProofMediaClaims_Attempt");
        b.HasIndex(x => new { x.ClaimState, x.UpdatedAtUtc })
            .HasDatabaseName("IX_GameProofMediaClaims_Reconciliation");

        b.HasOne<GameAttemptRow>().WithMany()
            .HasForeignKey(x => new { x.AttemptId, x.GameId, x.AuthenticatedPlayerUserId })
            .HasPrincipalKey(x => new { x.Id, x.GameId, x.PlayerUserId })
            .OnDelete(DeleteBehavior.NoAction);

        b.HasOne<GameProofSubmissionOperationRow>().WithMany()
            .HasForeignKey(x => new { x.SubmissionOperationId, x.GameId, x.AttemptId,
                x.AuthenticatedPlayerUserId, x.StepNumber, x.MediaProviderNamespace, x.CanonicalMediaId })
            .HasPrincipalKey(x => new { x.SubmissionOperationId, x.GameId, x.AttemptId,
                x.AuthenticatedPlayerUserId, x.StepNumber, x.MediaProviderNamespace, x.CanonicalMediaId })
            .OnDelete(DeleteBehavior.NoAction);

        b.HasOne<GameProofEvidenceRow>().WithMany()
            .HasForeignKey(x => new { x.EvidenceId, x.GameId, x.AttemptId, x.StepNumber })
            .HasPrincipalKey(x => new { x.Id, x.GameId, x.AttemptId, x.StepNumber })
            .OnDelete(DeleteBehavior.NoAction);
    }
}
