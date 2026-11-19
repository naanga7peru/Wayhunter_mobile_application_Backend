using System;

namespace Wayhunter.Application.Modules.Games.Dtos;

/// <summary>
/// Proposed isolated M5 proof submission operation binding.
/// Player identity must come from trusted server authentication.
/// Production integration requires owner approval.
/// </summary>
public sealed record GameProofSubmissionOperationBinding(
    Guid SubmissionOperationId,
    string AuthenticatedPlayerUserId,
    Guid GameId,
    Guid AttemptId,
    int StepNumber,
    string CaptureSessionId,
    string MediaProviderNamespace,
    string CanonicalMediaId
);
