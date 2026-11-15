
using System;
using System.Collections.Generic;
using Wayhunter.Domain.Modules.TravelPlanning.Entities;
using Wayhunter.Domain.Modules.TripSafety.Entities;

namespace Wayhunter.Application.Modules.TripSafety.Validators;

public static class TripMembershipAccessValidator
{
    // Validates access to private trip information.
    //
    // Allowed:
    // 1. Authenticated trip owner.
    // 2. Active member belonging to the same trip.
    //
    // Denied:
    // - Unauthenticated users.
    // - Removed members.
    // - Members of another trip.
    // - Users without membership.
    //
    // Authentication and membership data must
    // come from trusted backend sources.
    //
    // This validator does NOT grant GPS consent.

    public static void EnsureCanAccess(
        TripPlan? tripPlan,
        TripMember? member,
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

        string userId = authenticatedUserId.Trim();

        // The owner always retains access to
        // their own trip information.
        if (string.Equals(
            tripPlan.OwnerUserId,
            userId,
            StringComparison.Ordinal))
        {
            return;
        }

        // Other users require valid membership.
        if (member is null)
        {
            throw new UnauthorizedAccessException(
                "Trip membership is required.");
        }

        // A member of another trip cannot
        // access this trip.
        if (member.TripPlanId != tripPlan.Id)
        {
            throw new UnauthorizedAccessException(
                "Membership belongs to another trip.");
        }

        // Prevent one member from using another
        // member's identity or membership record.
        if (!string.Equals(
            member.MemberUserId,
            userId,
            StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException(
                "Membership belongs to another user.");
        }

        // Removed or inactive members
        // must immediately lose eligibility.
        if (!member.IsActive ||
            !member.CanAccessPrivateTrip())
        {
            throw new UnauthorizedAccessException(
                "Trip membership has been revoked.");
        }

        // Authorization successful.
        // No GPS consent is granted here.
    }
}
