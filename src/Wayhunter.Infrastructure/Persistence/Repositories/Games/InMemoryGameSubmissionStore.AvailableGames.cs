
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.Games.Dtos;
using Wayhunter.Application.Modules.Games.Interfaces;
using Wayhunter.Application.Modules.Games.Services;
using Wayhunter.Domain.Modules.Games.Enums;

namespace Wayhunter.Infrastructure.Persistence.Repositories.Games;

// =====================================
// M5-MS04-T01
// AVAILABLE GAMES QUERY
// IN-MEMORY PROTOTYPE
// =====================================

public sealed partial class InMemoryGameSubmissionStore
    : IAvailableGamesQueryStore
{
    public Task<IReadOnlyList<PublicGameSummaryDto>>
        GetAvailableGamesAsync(
            DateTimeOffset nowUtc,
            CancellationToken cancellationToken = default)
    {
        // Validate server UTC time.
        if (nowUtc == default ||
            nowUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "Valid UTC timestamp is required.",
                nameof(nowUtc));
        }

        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Only publicly available games.
            var availableGames = _games.Values
                .Where(game =>
                    // Admin-published games only.
                    game.Status == GameStatus.Published

                    // A real submission and approval
                    // must exist.
                    && game.SubmittedAtUtc.HasValue
                    && game.ApprovedAtUtc.HasValue
                    && game.ExpiresAtUtc.HasValue

                    // Publication must already
                    // have started.
                    && game.ApprovedAtUtc.Value <= nowUtc

                    // Public details must be valid.
                    && !string.IsNullOrWhiteSpace(game.Title)
                    && game.DurationMinutes is > 0

                    // Reuse existing Game domain
                    // eligibility and expiry rules.
                    && game.CanPlay(nowUtc))

                // Deterministic display order.
                .OrderBy(game => game.ApprovedAtUtc)
                .ThenBy(game => game.Id)

                // Safe fields only.
                .Select(game =>
                    GamePublicMapper.ToAvailableSummary(
                        game,
                        nowUtc))

                .ToArray();

            IReadOnlyList<PublicGameSummaryDto> result =
                availableGames;

            return Task.FromResult(result);
        }
    }
}
