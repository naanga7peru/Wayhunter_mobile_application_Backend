
using System;
using Wayhunter.Domain.Modules.Games.Enums;

namespace Wayhunter.Application.Modules.Games.Dtos;

public sealed record GameModerationAuditRecord(
    Guid DecisionId,
    Guid GameId,
    string AdminUserId,
    GameModerationAction Action,
    string Reason,
    DateTimeOffset OccurredAtUtc,
    GameStatus ResultingStatus,
    Guid CorrelationId
);
