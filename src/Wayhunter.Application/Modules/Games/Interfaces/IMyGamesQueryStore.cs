using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.Games.Dtos;

namespace Wayhunter.Application.Modules.Games.Interfaces;

public interface IMyGamesQueryStore
{
    Task<IReadOnlyList<MyGameDto>> GetMyGamesAsync(
        string creatorUserId,
        CancellationToken cancellationToken = default);
}
