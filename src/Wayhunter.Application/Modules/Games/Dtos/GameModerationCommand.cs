
using System;
using Wayhunter.Domain.Modules.Games.Enums;

namespace Wayhunter.Application.Modules.Games.Dtos;

public sealed record GameModerationCommand(
    Guid DecisionId,
    Guid GameId,
    string AdminUserId,
    GameModerationAction Action,
    string Reason,
    DateTimeOffset OccurredAtUtc,
    Guid CorrelationId
);
