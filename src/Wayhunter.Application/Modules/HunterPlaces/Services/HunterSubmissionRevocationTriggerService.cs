
using System;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.HunterPlaces.Interfaces;
using Wayhunter.Domain.Modules.HunterPlaces.Enums;

namespace Wayhunter.Application.Modules.HunterPlaces.Services;

/// <summary>
/// Handles trusted submission-status change notifications
/// by revoking outstanding Hunter capture sessions.
///
/// This service does not persist the status transition.
/// Production status updates and revocation must be
/// coordinated transactionally.
/// </summary>
public sealed class HunterSubmissionRevocationTriggerService
{
    private readonly IHunterCaptureSessionRevocationStore
        _revocationStore;

    public HunterSubmissionRevocationTriggerService(
        IHunterCaptureSessionRevocationStore revocationStore)
    {
        _revocationStore = revocationStore
            ?? throw new ArgumentNullException(
                nameof(revocationStore));
    }

    public async Task<int> HandleStatusChangedAsync(
        Guid submissionId,
        HunterSubmissionStatus newStatus,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (submissionId == Guid.Empty)
        {
            throw new ArgumentException(
                "Hunter submission ID is required.",
                nameof(submissionId));
        }

        if (!Enum.IsDefined(newStatus))
        {
            throw new ArgumentOutOfRangeException(
                nameof(newStatus),
                "Unknown Hunter submission status.");
        }

        // Draft remains eligible for new capture sessions.
        if (newStatus == HunterSubmissionStatus.Draft)
        {
            return 0;
        }

        // PendingReview, Held, Approved and Rejected
        // must not retain usable capture sessions.
        //
        // Repeated notifications are safe because the
        // store's revocation operation is idempotent.
        //
        // Never swallow storage errors: callers must
        // detect and handle a failed revocation.
        return await _revocationStore.RevokeBySubmissionIdAsync(
            submissionId,
            cancellationToken);
    }
}
