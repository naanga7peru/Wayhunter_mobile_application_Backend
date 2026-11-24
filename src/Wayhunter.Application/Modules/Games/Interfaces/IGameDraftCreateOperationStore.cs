using Wayhunter.Domain.Modules.Games.Entities;

namespace Wayhunter.Application.Modules.Games.Interfaces;

public interface IGameDraftCreateOperationStore
{
    Task<Game> CreateOrReplayAsync(
        Game game,
        string idempotencyKey,
        byte[] payloadHash,
        CancellationToken cancellationToken = default);
}
