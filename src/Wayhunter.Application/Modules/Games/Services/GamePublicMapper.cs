
using System;
using Wayhunter.Application.Modules.Games.Dtos;
using Wayhunter.Domain.Modules.Games.Entities;

namespace Wayhunter.Application.Modules.Games.Services;

public static class GamePublicMapper
{
    public static PublicGameSummaryDto ToAvailableSummary(
        Game game,
        DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(game);

        // Use server UTC, never the player's device clock.
        if (nowUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "UTC timestamp is required.",
                nameof(nowUtc));
        }

        // Only games currently eligible for play
        // may appear in the available-games response.
        if (!game.CanPlay(nowUtc))
        {
            throw new InvalidOperationException(
                "Game is not publicly available.");
        }

        // Reject incomplete legacy game records.
        if (string.IsNullOrWhiteSpace(game.Title) ||
            game.DurationMinutes is null or <= 0 ||
            !game.ApprovedAtUtc.HasValue ||
            !game.ExpiresAtUtc.HasValue)
        {
            throw new InvalidOperationException(
                "Game public details are incomplete.");
        }

        // Explicit allowlist: public fields only.
        return new PublicGameSummaryDto(
            game.Id,
            game.Title,
            game.Description,
            game.DurationMinutes.Value,
            game.ApprovedAtUtc.Value,
            game.ExpiresAtUtc.Value
        );
    }
}
