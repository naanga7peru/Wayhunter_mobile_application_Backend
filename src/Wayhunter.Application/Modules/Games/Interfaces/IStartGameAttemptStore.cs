
using System;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.Games.Dtos;

namespace Wayhunter.Application.Modules.Games.Interfaces;

public sealed class GameAttemptPersistenceUnavailableException(string message, Exception? innerException = null)
    : Exception(message, innerException);

/// <summary>
/// M5-MS04-T05
/// Contract for starting or resuming
/// a player's Game attempt.
/// </summary>
public interface IStartGameAttemptStore
{
    // The implementation must:
    //
    // 1. Load the requested Game.
    //
    // 2. Verify the Game is Admin-approved,
    //    published and currently playable.
    //
    // 3. Reject the Game creator.
    //
    // 4. Reject expired, flagged, disabled
    //    or winner-closed Games.
    //
    // 5. Check whether the player already
    //    has an active attempt for this Game.
    //
    // 6. Resume an eligible existing attempt
    //    instead of creating a duplicate.
    //
    // 7. Calculate the attempt deadline
    //    using authoritative server UTC time.
    //
    // 8. Never allow the attempt deadline
    //    to exceed the Game expiry.
    //
    // 9. Create an attempt and its initial
    //    progress together atomically.
    //
    // 10. Handle cancellation safely.
    //
    // SQL implementation remains blocked
    // until shared migration approval.

    Task<StartGameAttemptResultDto> StartOrResumeAsync(
        Guid gameId,
        string playerUserId,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default);
}
