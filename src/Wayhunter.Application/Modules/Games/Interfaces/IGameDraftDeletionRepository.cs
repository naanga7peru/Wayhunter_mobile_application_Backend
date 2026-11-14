
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Wayhunter.Application.Modules.Games.Interfaces;

public interface IGameDraftDeletionRepository : IGameRepository
{
    Task DeleteAsync(
        Guid gameId,
        CancellationToken cancellationToken = default);
}
