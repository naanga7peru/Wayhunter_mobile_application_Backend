using System;
using Wayhunter.Application.Modules.Games.Dtos;

namespace Wayhunter.Infrastructure.Persistence.Models.Games;

/// <summary>
/// Proposed M5 proof operation persistence model.
/// Physical schema and production activation require separate approval.
/// </summary>
public sealed class GameProofSubmissionOperationRow
{
    public Guid SubmissionOperationId { get; set; }
    public string AuthenticatedPlayerUserId { get; set; } = string.Empty;
    public Guid GameId { get; set; }
    public Guid AttemptId { get; set; }
    public int StepNumber { get; set; }
    public string CaptureSessionId { get; set; } = string.Empty;
    public string MediaProviderNamespace { get; set; } = string.Empty;
    public string CanonicalMediaId { get; set; } = string.Empty;
    public GameProofSubmissionOperationState State { get; set; } = GameProofSubmissionOperationState.Pending;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public DateTimeOffset? NextReconciliationAtUtc { get; set; }
    public string? ClaimToken { get; set; }
    public DateTimeOffset? LeaseExpiresAtUtc { get; set; }
    public DateTimeOffset? ProviderConsumptionConfirmedAtUtc { get; set; }
    public string? ProviderReceiptId { get; set; }
    public Guid? EvidenceId { get; set; }
    public string? LastErrorCode { get; set; }
    public int RecoveryAttemptCount { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
