using Wayhunter.Domain.Modules.Games.Enums;

namespace Wayhunter.Application.Modules.Games.Dtos;

/// <summary>Safe progress projection for the authenticated player's attempt.</summary>
public sealed record MyGameAttemptDto(
    Guid AttemptId,
    Guid GameId,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? DeadlineUtc,
    GameProgressStatus Status,
    int CurrentStepNumber,
    int TotalSteps);
