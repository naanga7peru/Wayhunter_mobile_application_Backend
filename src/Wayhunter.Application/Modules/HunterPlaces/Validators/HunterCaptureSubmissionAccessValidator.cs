
using System;
using Wayhunter.Application.Modules.HunterPlaces.Dtos;

namespace Wayhunter.Application.Modules.HunterPlaces.Validation;

/// <summary>
/// Validates whether an authenticated Hunter owns
/// the requested Hunter submission.
///
/// The snapshot must come from trusted backend storage.
/// Never accept an owner ID directly from Flutter.
/// </summary>
public static class HunterCaptureSubmissionAccessValidator
{
    public static bool CanAccess(
        string? authenticatedUserId,
        Guid requestedSubmissionId,
        HunterCaptureSubmissionAccessSnapshot? snapshot)
    {
        // Authentication identity is required.
        if (string.IsNullOrWhiteSpace(authenticatedUserId))
        {
            return false;
        }

        // The submission ID must be valid.
        if (requestedSubmissionId == Guid.Empty)
        {
            return false;
        }

        // Missing submissions are denied.
        if (snapshot is null)
        {
            return false;
        }

        // Prevent mismatched submission records.
        if (snapshot.SubmissionId != requestedSubmissionId)
        {
            return false;
        }

        // Stored ownership must be present.
        if (string.IsNullOrWhiteSpace(snapshot.OwnerUserId))
        {
            return false;
        }

        // Compare trusted account IDs exactly.
        return string.Equals(
            authenticatedUserId,
            snapshot.OwnerUserId,
            StringComparison.Ordinal);
    }
}
