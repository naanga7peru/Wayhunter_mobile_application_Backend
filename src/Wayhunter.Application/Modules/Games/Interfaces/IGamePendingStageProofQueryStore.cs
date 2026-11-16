
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.Games.Dtos;

namespace Wayhunter.Application.Modules.Games.Interfaces;

/// <summary>
/// M5 Game Admin pending stage-proof review queue.
/// Returns only evidence awaiting a trusted review decision.
/// </summary>
public interface IGamePendingStageProofQueryStore
{
    Task<IReadOnlyList<GamePendingStageProofDto>>
        GetPendingStageProofsAsync(
            CancellationToken cancellationToken = default);
}
