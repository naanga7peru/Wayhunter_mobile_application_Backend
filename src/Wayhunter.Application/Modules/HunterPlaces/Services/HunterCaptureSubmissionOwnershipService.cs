
using System;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.HunterPlaces.Interfaces;
using Wayhunter.Application.Modules.HunterPlaces.Validation;
using Wayhunter.Domain.Modules.HunterPlaces.Enums;

namespace Wayhunter.Application.Modules.HunterPlaces.Services;

/// <summary>
/// Validates trusted Hunter submission ownership.
///
/// Capture session creation additionally requires
/// the submission to be in Draft status.
/// </summary>
public sealed class HunterCaptureSubmissionOwnershipService
{
    private readonly IHunterCaptureSubmissionAccessReader _reader;

    public HunterCaptureSubmissionOwnershipService(
        IHunterCaptureSubmissionAccessReader reader)
    {
        _reader = reader ??
            throw new ArgumentNullException(nameof(reader));
    }

    // Preserves the existing ownership-only contract.
    public Task EnsureOwnedAsync(
        string? authenticatedUserId,
        Guid submissionId,
        CancellationToken cancellationToken = default)
    {
        return EnsureAccessAsync(
            authenticatedUserId,
            submissionId,
            requireDraft: false,
            cancellationToken: cancellationToken);
    }

    // Approved M4 rule: only Draft submissions
    // can request a new capture session.
    public Task EnsureCanStartCaptureAsync(
        string? authenticatedUserId,
        Guid submissionId,
        CancellationToken cancellationToken = default)
    {
        return EnsureAccessAsync(
            authenticatedUserId,
            submissionId,
            requireDraft: true,
            cancellationToken: cancellationToken);
    }

    private async Task EnsureAccessAsync(
        string? authenticatedUserId,
        Guid submissionId,
        bool requireDraft,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(authenticatedUserId) ||
            submissionId == Guid.Empty)
        {
            throw new UnauthorizedAccessException(
                "Hunter submission access denied.");
        }

        // Must be read from backend-controlled storage.
        var snapshot = await _reader.FindBySubmissionIdAsync(
            submissionId,
            cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        var owned =
            HunterCaptureSubmissionAccessValidator.CanAccess(
                authenticatedUserId,
                submissionId,
                snapshot);

        var eligible =
            !requireDraft ||
            snapshot?.Status == HunterSubmissionStatus.Draft;

        if (!owned || !eligible)
        {
            // Same error for missing, foreign and
            // ineligible submissions.
            throw new UnauthorizedAccessException(
                "Hunter submission access denied.");
        }
    }
}
