
using System;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.HunterPlaces.Interfaces;
using Wayhunter.Application.Modules.HunterPlaces.Validation;

namespace Wayhunter.Application.Modules.HunterPlaces.Services;

/// <summary>
/// Checks whether a trusted authenticated user owns
/// a Hunter submission.
///
/// The authenticated user ID must come from backend
/// authentication, never directly from Flutter.
/// </summary>
public sealed class HunterCaptureSubmissionOwnershipService
{
    private readonly IHunterCaptureSubmissionAccessReader _reader;

    public HunterCaptureSubmissionOwnershipService(
        IHunterCaptureSubmissionAccessReader reader)
    {
        _reader = reader
            ?? throw new ArgumentNullException(nameof(reader));
    }

    public async Task EnsureOwnedAsync(
        string? authenticatedUserId,
        Guid submissionId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(authenticatedUserId) ||
            submissionId == Guid.Empty)
        {
            throw new UnauthorizedAccessException(
                "Hunter submission access denied.");
        }

        var snapshot = await _reader.FindBySubmissionIdAsync(
            submissionId,
            cancellationToken);

        var allowed =
            HunterCaptureSubmissionAccessValidator.CanAccess(
                authenticatedUserId,
                submissionId,
                snapshot);

        if (!allowed)
        {
            throw new UnauthorizedAccessException(
                "Hunter submission access denied.");
        }
    }
}
