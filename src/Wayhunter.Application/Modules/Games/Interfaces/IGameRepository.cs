
using System;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Domain.Modules.Games.Entities;

namespace Wayhunter.Application.Modules.Games.Interfaces;

public interface IGameRepository
{
    Task<Game?> GetByIdAsync(
        Guid gameId,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        Game game,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        Game game,
        CancellationToken cancellationToken = default);
}
