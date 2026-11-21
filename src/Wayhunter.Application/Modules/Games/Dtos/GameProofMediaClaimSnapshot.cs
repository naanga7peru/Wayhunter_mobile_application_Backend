using System;

namespace Wayhunter.Application.Modules.Games.Dtos;

// Proposed pure code-only representation, not a provider authorization token.
public enum GameProofMediaClaimOrigin : byte
{
    NewOperation = 1,
    VerifiedLegacyEvidence = 2
}

public enum GameProofMediaClaimState : byte
{
    Reserved = 0,
    ProviderConsumed = 1,
    EvidenceCommitted = 2,
    CancelledUnusedTombstone = 3,
    LegacyIndexed = 4
}

// Matches the 15 non-rowversion columns in accepted registry Design v3.
// Provider receipt stays on the operation, never in this registry snapshot.
public sealed record GameProofMediaClaimSnapshot(
    Guid MediaClaimId,
    string MediaProviderNamespace,
    string CanonicalMediaId,
    Guid? SubmissionOperationId,
    Guid? EvidenceId,
    Guid GameId,
    Guid AttemptId,
    int StepNumber,
    string AuthenticatedPlayerUserId,
    GameProofMediaClaimOrigin ClaimOrigin,
    GameProofMediaClaimState ClaimState,
    DateTimeOffset? ProviderConsumedAtUtc,
    DateTimeOffset? EvidenceCommittedAtUtc,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
