
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.Games.Dtos;
using Wayhunter.Application.Modules.Games.Interfaces;

namespace Wayhunter.Application.Modules.Games.Services;

/// <summary>
/// M5 Game Admin pending stage-proof review query.
/// Application service over the query-store contract.
/// </summary>
public sealed class GetPendingGameStageProofsService
{
    private readonly IGamePendingStageProofQueryStore _store;

    public GetPendingGameStageProofsService(
        IGamePendingStageProofQueryStore store)
    {
        _store = store ??
            throw new ArgumentNullException(nameof(store));
    }

    public async Task<IReadOnlyList<GamePendingStageProofDto>>
        GetAsync(
            CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return await _store.GetPendingStageProofsAsync(
            cancellationToken);
    }
}
