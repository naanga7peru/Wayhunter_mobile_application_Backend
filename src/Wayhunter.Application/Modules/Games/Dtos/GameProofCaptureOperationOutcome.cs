using System;

namespace Wayhunter.Application.Modules.Games.Dtos;

/// <summary>
/// Proposed trusted provider reconciliation outcome.
/// Final semantics require Capture Owner approval.
/// Unknown must never authorize a new capture consumption.
/// </summary>
public enum GameProofCaptureOperationOutcomeStatus
{
    Unknown = 0,
    Consumed = 1,
    Rejected = 2
}

public sealed record GameProofCaptureOperationOutcome(
    Guid SubmissionOperationId,
    GameProofCaptureOperationOutcomeStatus Status,
    string? ProviderReceiptId
);
