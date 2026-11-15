
using System;
using System.Collections.Generic;
using Wayhunter.Domain.Modules.TripSafety.Entities;
using Wayhunter.Domain.Modules.TripSafety.Enums;

namespace Wayhunter.Application.Modules.TripSafety.Validators;

public static class ActiveSessionLocationEventValidator
{
    // Validates the active session before publishing
    // any live trip location event.
    //
    // Caller must separately verify:
    // - Authenticated user and trip membership
    // - Explicit GPS sharing consent
    // - Recipient group access
    //
    // Planned-route hazard alerts are separate.

    public static void EnsureCanPublish(
        LiveTripSession? session,
        Guid expectedTripPlanId)
    {
        if (expectedTripPlanId == Guid.Empty)
        {
            throw new ArgumentException(
                "Valid Trip Plan ID is required.",
                nameof(expectedTripPlanId));
        }

        if (session is null)
        {
            throw new KeyNotFoundException(
                "Live trip session was not found.");
        }

        if (session.TripPlanId != expectedTripPlanId)
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
