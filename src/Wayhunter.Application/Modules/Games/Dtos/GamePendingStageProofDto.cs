
using System;

namespace Wayhunter.Application.Modules.Games.Dtos;

/// <summary>
/// M5 Game Admin pending stage-proof review queue.
/// Contains the submitted evidence and its
/// authoritative Game attempt context.
/// </summary>
public sealed record GamePendingStageProofDto(
    Guid GameId,
    string GameTitle,
    string PlayerUserId,
    Guid AttemptId,
    Guid EvidenceId,
    int StepNumber,
    string MediaId,
    DateTimeOffset SubmittedAtUtc,
    DateTimeOffset AttemptStartedAtUtc,
    DateTimeOffset AttemptDeadlineUtc);
