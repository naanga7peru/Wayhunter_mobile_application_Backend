namespace Wayhunter.Application.Modules.Games.Dtos;

/// <summary>Safe public metadata for a currently playable Game.</summary>
public sealed record PublicGameDetailsDto(
    Guid GameId,
    string Title,
    string? Description,
    int DurationMinutes,
    DateTimeOffset PublishedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    int StageCount);
