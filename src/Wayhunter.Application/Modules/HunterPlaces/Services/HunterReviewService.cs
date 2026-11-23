using System;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.HunterPlaces.Dtos;
using Wayhunter.Application.Modules.HunterPlaces.Interfaces;
using Wayhunter.Application.Modules.HunterPlaces.Validation;
using Wayhunter.Domain.Modules.HunterPlaces.Entities;
using Wayhunter.Domain.Modules.HunterPlaces.Enums;

namespace Wayhunter.Application.Modules.HunterPlaces.Services;

/// <summary>
/// Coordinates the existing M4 Admin review contract.
/// The reviewer ID must be supplied by a trusted, already-authorized
/// backend caller; this service is not an authentication boundary.
/// </summary>
public sealed class HunterReviewService : IHunterReviewService
{
    private const int MaximumReviewerIdLength = 256;
    private const int MaximumReasonLength = 2000;

    private readonly IHunterReviewStore _store;
    private readonly TimeProvider _timeProvider;

    public HunterReviewService(
        IHunterReviewStore store,
        TimeProvider timeProvider)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _timeProvider = timeProvider ??
            throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<HunterSubmissionResponseDto> ReviewAsync(
        string authenticatedAdminUserId,
        HunterReviewRequestDto request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);

        if (!Guid.TryParseExact(authenticatedAdminUserId, "D", out var reviewerId) ||
            reviewerId == Guid.Empty ||
            authenticatedAdminUserId.Length > MaximumReviewerIdLength)
        {
            throw new UnauthorizedAccessException(
                "A trusted Admin identity is required.");
        }

        // Rejection is enabled for this development contract and requires
        // an explicit reason. Held remains disabled.
        if (request.ResultingStatus is not
            (HunterSubmissionStatus.Approved or HunterSubmissionStatus.Rejected))
        {
            throw new InvalidOperationException(
                "Only the approved Hunter decision is currently enabled.");
        }

        var errors = HunterReviewRequestValidator.Validate(request);
        if (errors.Count > 0)
        {
            throw new ArgumentException(
                string.Join("; ", errors),
                nameof(request));
        }

        if (request.Reason?.Length > MaximumReasonLength)
        {
            throw new ArgumentException(
                "Review reason exceeds the supported length.",
                nameof(request));
        }

        var reviewedAtUtc = _timeProvider.GetUtcNow();
        if (reviewedAtUtc == default ||
            reviewedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new InvalidOperationException(
                "The server clock must provide a UTC review time.");
        }

        var decision = new HunterReviewDecision
        {
            HunterSubmissionId = request.HunterSubmissionId,
            ReviewerUserId = reviewerId.ToString("D"),
            ResultingStatus = request.ResultingStatus,
            Reason = request.Reason,
            ReviewedAtUtc = reviewedAtUtc
        };

        return await _store.TryReviewPendingAsync(
                   decision,
                   cancellationToken)
               ?? throw new InvalidOperationException(
                   "Hunter submission is missing or no longer pending review.");
    }
}
