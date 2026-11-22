using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.Games.Interfaces;
using Wayhunter.Domain.Modules.Games.Enums;

namespace Wayhunter.Infrastructure.Persistence.Repositories.Games;

public sealed partial class InMemoryGameSubmissionStore
    : IExpiredGameArchiveStore
{
    public Task<int> ArchiveExpiredGamesAsync(
        DateTimeOffset nowUtc,
        int maxCount,
        CancellationToken cancellationToken = default)
    {
        if (nowUtc == default || nowUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "A valid UTC timestamp is required.", nameof(nowUtc));
        }

        if (maxCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxCount));
        }

        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var expired = _games.Values
                .Where(game => game.Status == GameStatus.Published
                    && game.ExpiresAtUtc.HasValue
                    && game.ExpiresAtUtc.Value <= nowUtc)
                .OrderBy(game => game.ExpiresAtUtc)
                .ThenBy(game => game.Id)
                .Take(maxCount)
                .ToArray();

            var archivedCount = 0;
            foreach (var game in expired)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (game.ArchiveIfExpired(nowUtc))
                    archivedCount++;
            }

            return Task.FromResult(archivedCount);
        }
    }
}
