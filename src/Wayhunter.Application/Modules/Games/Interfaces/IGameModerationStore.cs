
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.Games.Dtos;

namespace Wayhunter.Application.Modules.Games.Interfaces;

public interface IGameModerationStore
{
    // M5-MS03-T05
    //
    // Persistent implementation must:
    // 1. Verify authenticated Admin authorization.
    // 2. Load the current Game state.
    // 3. Apply the requested moderation action.
    // 4. Save an immutable audit record.
    // 5. Save Game and audit atomically.
    // 6. Reject conflicting duplicate decisions.
    // 7. Remove rejected games from pending queue.
    // 8. Never create a reward for rejection.
    // 9. Block flagged/disabled games from play.
    //
    // This interface does not implement persistence.

    Task<GameModerationAuditRecord> ApplyAsync(
        GameModerationCommand command,
        CancellationToken cancellationToken = default);
}
