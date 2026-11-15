
using System;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.Games.Dtos;

namespace Wayhunter.Application.Modules.Games.Interfaces;

/// <summary>
/// M5-MS05-T01
/// Contract for retrieving the current
/// playable stage of a Game attempt.
/// </summary>
public interface ICurrentGameStageQueryStore
{
    // Implementation requirements:
    //
    // 1. Validate Game ID and Attempt ID.
    //
    // 2. Validate authenticated Player User ID.
    //
    // 3. Validate authoritative server UTC time.
    //
    // 4. Load the requested Game and Attempt.
    //
    // 5. Verify the Attempt belongs to
    //    the requested Game.
    //
    // 6. Verify the Attempt belongs to
    //    the authenticated player.
    //
    // 7. Reject unavailable Games:
    //    expired, flagged, disabled,
    //    rejected or winner-closed.
    //
    // 8. Reject attempts past their deadline.
    //
    // 9. Require a valid active
    //    GameProgress state.
    //
    // 10. Read only CurrentStepNumber.
    //
    // 11. Return only hints belonging
    //     to the current GameStep.
    //
    // 12. Never expose future stage hints.
    //
    // 13. Support cancellation.
    //
    // SQL implementation remains blocked
    // until shared migration approval.

    Task<CurrentGameStageDto> GetCurrentStageAsync(
        Guid gameId,
        Guid attemptId,
        string playerUserId,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default);
}
