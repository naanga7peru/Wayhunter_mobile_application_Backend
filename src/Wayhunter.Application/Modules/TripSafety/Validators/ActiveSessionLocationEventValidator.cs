
using System;
using System.Collections.Generic;
using Wayhunter.Domain.Modules.TripSafety.Entities;
using Wayhunter.Domain.Modules.TripSafety.Enums;

namespace Wayhunter.Application.Modules.TripSafety.Validators;

public static class ActiveSessionLocationEventValidator
{
    // Validate the session gate before publishing
    // any live trip-location event.
    //
    // Caller must separately verify:
    // - Authenticated user / trip membership
    // - Explicit, current GPS sharing consent
    // - Permission to access the recipient group
    //
    // This must NOT be used to block planned-route
    // hazard alerts. Those may apply before a trip starts.
    public static void EnsureCanPublish(
        LiveTripSession? session,
        string? expectedTripPlanId)
    {
        if (string.IsNullOrWhiteSpace(expectedTripPlanId))
        {
            throw new ArgumentException(
                "Trip Plan ID is required.",
                nameof(expectedTripPlanId));
        }

        if (session is null)
        {
            throw new KeyNotFoundException(
                "Live trip session was not found.");
        }

        if (!string.Equals(
            session.TripPlanId,
            expectedTripPlanId.Trim(),
            StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException(
                "Session does not belong to this trip.");
        }

        if (session.Status != LiveTripStatus.Active)
        {
            throw new InvalidOperationException(
                "Location events require an active trip.");
        }
    }
}
