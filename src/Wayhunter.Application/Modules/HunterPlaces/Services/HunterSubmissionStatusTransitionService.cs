
using System;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.HunterPlaces.Interfaces;
using Wayhunter.Domain.Modules.HunterPlaces.Enums;

namespace Wayhunter.Application.Modules.HunterPlaces.Services;

/// <summary>
/// Application-level coordination for Hunter submission
/// status changes.
///
/// Authorization must be completed by trusted backend
/// callers before invoking these operations.
///
/// The transition store must persist the status change
/// and revoke capture sessions atomically.
///
/// No production persistence is registered here.
/// </summary>
public sealed class HunterSubmissionStatusTransitionService
{
    private readonly IHunterSubmissionStatusTransitionStore
        _transitionStore;

    public HunterSubmissionStatusTransitionService(
        IHunterSubmissionStatusTransitionStore transitionStore)
    {
        _transitionStore = transitionStore
            ?? throw new ArgumentNullException(
                nameof(transitionStore));
    }

    /// <summary>
    /// Submits an existing Draft for Admin review.
    /// </summary>
    public Task<bool> SubmitDraftAsync(
        Guid submissionId,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(submissionId, cancellationToken);

        return _transitionStore
            .TryTransitionAndRevokeCaptureSessionsAsync(
                submissionId,
                HunterSubmissionStatus.Draft,
                HunterSubmissionStatus.PendingReview,
                cancellationToken);
    }

    /// <summary>
    /// Processes an Admin decision for a PendingReview
    /// submission.
    ///
    /// Admin authentication, authorization, review
    /// reason validation and audit recording are
    /// responsibilities of the calling review flow.
    ///
    /// Additional Held/re-review transitions require
    /// separately confirmed business rules.
    /// </summary>
    public Task<bool> ApplyAdminDecisionAsync(
        Guid submissionId,
        HunterSubmissionStatus resultingStatus,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(submissionId, cancellationToken);

        if (resultingStatus is not
            (HunterSubmissionStatus.Held
            or HunterSubmissionStatus.Approved
            or HunterSubmissionStatus.Rejected))
        {
            throw new ArgumentOutOfRangeException(
                nameof(resultingStatus),
                "Admin decision must be Held, Approved or Rejected.");
        }

        return _transitionStore
            .TryTransitionAndRevokeCaptureSessionsAsync(
                submissionId,
                HunterSubmissionStatus.PendingReview,
                resultingStatus,
                cancellationToken);
    }

    private static void ValidateRequest(
        Guid submissionId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (submissionId == Guid.Empty)
        {
            throw new ArgumentException(
                "Hunter submission ID is required.",
                nameof(submissionId));
        }
    }
}
