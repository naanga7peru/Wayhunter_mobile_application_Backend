using System;
using Wayhunter.Application.Modules.Games.Dtos;

namespace Wayhunter.Application.Modules.Games.Validators;

/// <summary>
/// Pure proposed registry consistency rules for Design v3. These are NOT a
/// substitute for trusted M4 canonicalization, SQL constraints, permissions,
/// authoritative receipt verification, or a DB-controlled transaction.
/// </summary>
public static class GameProofMediaClaimRules
{
    public static void Validate(GameProofMediaClaimSnapshot claim)
    {
        ArgumentNullException.ThrowIfNull(claim);

        if (claim.MediaClaimId == Guid.Empty || claim.GameId == Guid.Empty ||
            claim.AttemptId == Guid.Empty || claim.StepNumber < 1 ||
            claim.SubmissionOperationId == Guid.Empty || claim.EvidenceId == Guid.Empty)
            throw new ArgumentException("Invalid claim, operation, evidence, attempt or step identifier.", nameof(claim));

        ValidateText(claim.MediaProviderNamespace, 64, nameof(claim.MediaProviderNamespace));
        ValidateText(claim.CanonicalMediaId, 256, nameof(claim.CanonicalMediaId));
        ValidateText(claim.AuthenticatedPlayerUserId, 256, nameof(claim.AuthenticatedPlayerUserId));

        if (claim.ClaimOrigin is not (GameProofMediaClaimOrigin.NewOperation or
            GameProofMediaClaimOrigin.VerifiedLegacyEvidence))
            throw new ArgumentException("Invalid claim origin.", nameof(claim));

        if (claim.ClaimState is < GameProofMediaClaimState.Reserved or
            > GameProofMediaClaimState.LegacyIndexed)
            throw new ArgumentException("Invalid claim state.", nameof(claim));

        RequireUtc(claim.CreatedAtUtc);
        RequireUtc(claim.UpdatedAtUtc);
        if (claim.ProviderConsumedAtUtc.HasValue)
            RequireUtc(claim.ProviderConsumedAtUtc.Value);
        if (claim.EvidenceCommittedAtUtc.HasValue)
            RequireUtc(claim.EvidenceCommittedAtUtc.Value);

        if (claim.UpdatedAtUtc < claim.CreatedAtUtc ||
            (claim.ProviderConsumedAtUtc.HasValue &&
             claim.ProviderConsumedAtUtc.Value < claim.CreatedAtUtc) ||
            (claim.EvidenceCommittedAtUtc.HasValue &&
             (!claim.ProviderConsumedAtUtc.HasValue ||
              claim.EvidenceCommittedAtUtc.Value < claim.ProviderConsumedAtUtc.Value ||
              claim.EvidenceCommittedAtUtc.Value > claim.UpdatedAtUtc)))
            throw new ArgumentException("Invalid server timestamp order.", nameof(claim));

        if (claim.ClaimOrigin == GameProofMediaClaimOrigin.VerifiedLegacyEvidence)
        {
            if (claim.SubmissionOperationId is not null || claim.EvidenceId is null ||
                claim.ClaimState != GameProofMediaClaimState.LegacyIndexed ||
                claim.ProviderConsumedAtUtc is not null || claim.EvidenceCommittedAtUtc is not null)
                throw new ArgumentException("Legacy claim must stay terminal and evidence-linked.", nameof(claim));
            return;
        }

        if (claim.SubmissionOperationId is null)
            throw new ArgumentException("New operation requires immutable operation ID.", nameof(claim));

        bool correct = claim.ClaimState switch
        {
            GameProofMediaClaimState.Reserved or GameProofMediaClaimState.CancelledUnusedTombstone =>
                claim.EvidenceId is null && claim.ProviderConsumedAtUtc is null &&
                claim.EvidenceCommittedAtUtc is null,
            GameProofMediaClaimState.ProviderConsumed =>
                claim.EvidenceId is null && claim.ProviderConsumedAtUtc is not null &&
                claim.EvidenceCommittedAtUtc is null,
            GameProofMediaClaimState.EvidenceCommitted =>
                claim.EvidenceId is not null && claim.ProviderConsumedAtUtc is not null &&
                claim.EvidenceCommittedAtUtc is not null,
            _ => false
        };
        if (!correct)
            throw new ArgumentException("Claim state and associated fields disagree.", nameof(claim));
    }

    public static void EnsureSameImmutableOwner(
        GameProofMediaClaimSnapshot before,
        GameProofMediaClaimSnapshot after)
    {
        Validate(before);
        Validate(after);

        if (before.MediaClaimId != after.MediaClaimId ||
            !string.Equals(before.MediaProviderNamespace, after.MediaProviderNamespace, StringComparison.Ordinal) ||
            !string.Equals(before.CanonicalMediaId, after.CanonicalMediaId, StringComparison.Ordinal) ||
            before.SubmissionOperationId != after.SubmissionOperationId ||
            before.GameId != after.GameId || before.AttemptId != after.AttemptId ||
            before.StepNumber != after.StepNumber ||
            !string.Equals(before.AuthenticatedPlayerUserId, after.AuthenticatedPlayerUserId, StringComparison.Ordinal) ||
            before.ClaimOrigin != after.ClaimOrigin || before.CreatedAtUtc != after.CreatedAtUtc)
            throw new InvalidOperationException("Media claim's immutable owner cannot change.");
    }

    /// <summary>
    /// Consistency guard only: caller must independently prove a confirmed M4
    /// receipt before 0->1, definitively unconsumed before 0->3, and an atomic
    /// evidence/progress SQL commit before 1->2. This method authorizes NONE.
    /// Same-state requests are read-only and must not rewrite timestamps.
    /// </summary>
    public static void EnsureAllowedTransition(
        GameProofMediaClaimSnapshot before,
        GameProofMediaClaimSnapshot after)
    {
        EnsureSameImmutableOwner(before, after);

        if (before.ClaimState == after.ClaimState)
        {
            if (before != after)
                throw new InvalidOperationException("Idempotent same-state retries are read-only.");
            return;
        }

        if (after.UpdatedAtUtc < before.UpdatedAtUtc)
            throw new InvalidOperationException("UpdatedAtUtc cannot move backwards.");

        if (before.ClaimState == GameProofMediaClaimState.Reserved &&
            after.ClaimState is GameProofMediaClaimState.ProviderConsumed or
                GameProofMediaClaimState.CancelledUnusedTombstone)
            return;

        if (before.ClaimState == GameProofMediaClaimState.ProviderConsumed &&
            after.ClaimState == GameProofMediaClaimState.EvidenceCommitted &&
            before.ProviderConsumedAtUtc == after.ProviderConsumedAtUtc)
            return;

        throw new InvalidOperationException("Claim state regression, tombstone release or illegal transition.");
    }

    private static void ValidateText(string? text, int max, string field)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length > max)
            throw new ArgumentException($"Invalid {field} (required; max {max} UTF-16 code units).", field);
    }

    private static void RequireUtc(DateTimeOffset utc)
    {
        if (utc == default || utc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Trusted server UTC time is required.");
    }
}
