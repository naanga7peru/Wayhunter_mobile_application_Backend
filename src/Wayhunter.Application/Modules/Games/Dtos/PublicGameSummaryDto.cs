
using System;

namespace Wayhunter.Application.Modules.Games.Dtos;

public sealed record PublicGameSummaryDto(
    Guid GameId,
    string Title,
    string? Description,
    int DurationMinutes,
    DateTimeOffset PublishedAtUtc,
    DateTimeOffset ExpiresAtUtc
);
