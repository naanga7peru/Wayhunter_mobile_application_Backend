using System;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.Games.Interfaces;

namespace Wayhunter.Application.Modules.Games.Services;

public sealed class ArchiveExpiredGamesService
{
    private readonly IExpiredGameArchiveStore _store;
    private readonly TimeProvider _timeProvider;

    public ArchiveExpiredGamesService(
        IExpiredGameArchiveStore store,
        TimeProvider timeProvider)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _timeProvider = timeProvider
            ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public Task<int> ArchiveBatchAsync(
        int maxCount,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (maxCount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxCount),
                "Archive batch size must be positive.");
        }

        var nowUtc = _timeProvider.GetUtcNow();
        if (nowUtc.Offset != TimeSpan.Zero)
        {
            throw new InvalidOperationException(
                "The configured clock must return UTC.");
        }

        return _store.ArchiveExpiredGamesAsync(
            nowUtc, maxCount, cancellationToken);
    }
}
