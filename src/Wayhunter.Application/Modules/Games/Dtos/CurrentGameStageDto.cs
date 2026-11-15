
using System;
using System.Collections.Generic;
using Wayhunter.Domain.Modules.Games.Enums;

namespace Wayhunter.Application.Modules.Games.Dtos;

/// <summary>
/// M5-MS05-T01
/// Represents only the current stage
/// visible to the authorized player.
/// </summary>
public sealed record CurrentGameStageDto(
    Guid AttemptId,
    Guid GameId,
    Guid GameStepId,
    int CurrentStepNumber,
    int TotalSteps,
    GameProgressStatus ProgressStatus,
    DateTimeOffset DeadlineUtc,
    IReadOnlyList<CurrentStageHintDto> Hints
);

/// <summary>
/// A text or photo clue belonging
/// to the current stage only.
/// </summary>
public sealed record CurrentStageHintDto(
    string? TextHint,
    string? PhotoMediaId
);
