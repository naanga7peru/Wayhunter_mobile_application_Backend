
using System;

namespace Wayhunter.Application.Modules.Games.Dtos;

/// <summary>
/// M5-MS04-T05
/// Result returned when an eligible player
/// starts or resumes a Game attempt.
/// </summary>
public sealed record StartGameAttemptResultDto(
    Guid AttemptId,
    Guid GameId,
    string PlayerUserId,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset DeadlineUtc,
    int CurrentStepNumber,
    bool ResumedExistingAttempt
);
