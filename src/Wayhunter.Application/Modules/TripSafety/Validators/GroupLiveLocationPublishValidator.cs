
using System;
using Wayhunter.Domain.Modules.TripSafety.Entities;

namespace Wayhunter.Application.Modules.TripSafety.Validators;

public static class GroupLiveLocationPublishValidator
{
    // This is a validation rule, not an API endpoint.
    //
    // Participant authorization must be checked
    // using trusted backend data.
    //
    // Invitation acceptance alone must never
    // enable GPS sharing.

    public static void EnsureCanPublish(
        LiveTripSession? session,
        TripGpsConsent? consent,
        Guid expectedTripPlanId,
        string? authenticatedUserId,
        bool isAuthorizedTripParticipant)
    {
        if (string.IsNullOrWhiteSpace(authenticatedUserId))
        {
            throw new UnauthorizedAccessException(
                "Authenticated user is required.");
        }

        if (!isAuthorizedTripParticipant)
        {
            throw new UnauthorizedAccessException(
                "User is not an authorized trip participant.");
        }

        ActiveSessionLocationEventValidator.EnsureCanPublish(
            session,
            expectedTripPlanId);

        if (consent is null)
        {
            throw new UnauthorizedAccessException(
                "GPS sharing consent is required.");
        }

        if (consent.SessionId != session!.SessionId)
        {
            throw new UnauthorizedAccessException(
                "GPS consent belongs to another session.");
        }

        if (!string.Equals(
            consent.UserId,
            authenticatedUserId.Trim(),
            StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException(
                "GPS consent belongs to another user.");
        }

        if (!consent.IsGranted ||
            !consent.GrantedAtUtc.HasValue ||
            consent.RevokedAtUtc.HasValue)
        {
            throw new UnauthorizedAccessException(
                "Active GPS sharing consent is required.");
        }
    }
}
