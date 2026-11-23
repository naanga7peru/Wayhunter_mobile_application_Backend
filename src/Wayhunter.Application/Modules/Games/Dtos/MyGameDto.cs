using System;
using Wayhunter.Domain.Modules.Games.Enums;

namespace Wayhunter.Application.Modules.Games.Dtos;

public sealed record MyGameDto(
    Guid GameId,
    string? Title,
    GameStatus Status,
    DateTimeOffset? ApprovedAtUtc,
    DateTimeOffset? ExpiresAtUtc);
