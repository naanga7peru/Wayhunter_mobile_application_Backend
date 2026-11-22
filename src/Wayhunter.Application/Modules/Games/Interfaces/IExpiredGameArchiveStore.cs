using System;
using System.Threading;
using System.Threading.Tasks;

namespace Wayhunter.Application.Modules.Games.Interfaces;

public interface IExpiredGameArchiveStore
{
    Task<int> ArchiveExpiredGamesAsync(
        DateTimeOffset nowUtc,
        int maxCount,
        CancellationToken cancellationToken = default);
}
