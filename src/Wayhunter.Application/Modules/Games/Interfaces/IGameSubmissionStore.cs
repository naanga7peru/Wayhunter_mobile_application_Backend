
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.Games.Dtos;
using Wayhunter.Application.Modules.IdentitySocial.Dtos;

namespace Wayhunter.Application.Modules.Games.Interfaces;

public interface IGameSubmissionStore
{
    // Implementation must:
    //
    // 1. Load the Game using identity.ItemId.
    // 2. Check Draft status and creator ownership.
    // 3. Call Game.SubmitForAdminReview().
    // 4. Save the PendingApproval Game and queue item
    //    in one atomic transaction.
    // 5. Preserve the creator name snapshot.
    // 6. Reject concurrent or duplicate submissions.
    //
    // SQL implementation will be added after
    // shared database migration approval.

    Task<GameAdminQueueItemDto> SubmitDraftAndEnqueueAsync(
        ApprovalIdentitySnapshotDto identity,
        CancellationToken cancellationToken = default);
}
