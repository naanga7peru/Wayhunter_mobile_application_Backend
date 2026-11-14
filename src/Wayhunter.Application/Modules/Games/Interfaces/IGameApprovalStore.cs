
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.CoinsWallet.Dtos;
using Wayhunter.Application.Modules.Games.Dtos;

namespace Wayhunter.Application.Modules.Games.Interfaces;

public interface IGameApprovalStore
{
    // =====================================
    // M5-MS03-T04
    // ATOMIC GAME APPROVAL CONTRACT
    // =====================================

    // Future persistent implementation must:
    //
    // 1. Verify the authenticated Admin identity
    //    and authorization before execution.
    //
    // 2. Load the Game and its Admin review record.
    //
    // 3. Require a real submitted game:
    //    SubmittedAtUtc must exist.
    //
    // 4. Verify PendingApproval status and
    //    approved review decision.
    //
    // 5. Verify required clue media and game
    //    eligibility before publication.
    //
    // 6. Call Game.ApproveCreation() using
    //    the authoritative server UTC time.
    //
    // 7. Create the +100 Coins reward event
    //    using GameCreatorApprovalRewardFactory.
    //
    // 8. Persist the Game and reward outbox event
    //    atomically in one database transaction.
    //
    // 9. Guarantee one creator reward source
    //    per GameId and reward type.
    //
    // 10. Make repeated requests idempotent:
    //     return the existing event for a valid
    //     retry; reject conflicting approvals.
    //
    // 11. Update the Admin pending queue
    //     consistently with the review decision.
    //
    // M6 will process the reward event and
    // credit Coins through its authoritative ledger.
    //
    // No Coins are credited by this interface.

    Task<CoinRewardGrantRequest>
        ApproveAndEnqueueCreatorRewardAsync(
            GameApprovalCommand command,
            CancellationToken cancellationToken = default);
}
