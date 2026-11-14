
using System;
using System.Collections.Generic;
using Wayhunter.Domain.Modules.TravelPlanning.Entities;
using Wayhunter.Domain.Modules.TravelPlanning.Enums;

namespace Wayhunter.Application.Modules.TripSafety.Validators;

public static class StartTripEligibilityValidator
{
    // Draft policy: the authenticated trip owner
    // may start a non-cancelled trip.
    //
    // This validator does not load the trip,
    // authenticate a user or start a session.
    public static void EnsureOwnerMayStart(
        TripPlan? tripPlan,
        string? authenticatedUserId)
    {
        if (string.IsNullOrWhiteSpace(authenticatedUserId))
        {
            throw new UnauthorizedAccessException(
                "Authenticated user is required.");
        }

        if (tripPlan is null)
        {
            throw new KeyNotFoundException(
                "Trip plan was not found.");
        }

        if (!string.Equals(
            tripPlan.OwnerUserId,
            authenticatedUserId.Trim(),
            StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException(
                "Only the trip owner can start this trip.");
        }

        if (tripPlan.DraftStatus == TripDraftStatus.Cancelled)
        {
            throw new InvalidOperationException(
                "Cancelled trip plans cannot be started.");
        }
    }
}
