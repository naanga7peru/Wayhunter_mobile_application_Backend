using System;

namespace Wayhunter.Application.Modules.Games.Dtos;

/// <summary>
/// Isolated durable snapshot of an M5 proof submission operation.
/// Returned values must originate from trusted persistent storage.
/// Production integration requires separate approval.
/// </summary>
public sealed record GameProofSubmissionOperationSnapshot(
    GameProofSubmissionOperationBinding Binding,
    GameProofSubmissionOperationState State,
    Guid? EvidenceId,
    string? ClaimToken,
    DateTimeOffset? LeaseExpiresAtUtc,
    DateTimeOffset? ProviderConsumptionConfirmedAtUtc,
    DateTimeOffset? NextReconciliationAtUtc,
    int RecoveryAttemptCount,
    string? LastErrorCode,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    string? ProviderReceiptId = null
);
