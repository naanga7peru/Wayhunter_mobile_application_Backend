
using System;
using System.Collections.Generic;
using Wayhunter.Domain.Modules.TravelPlanning.Entities;
using Wayhunter.Domain.Modules.TravelPlanning.Enums;
using Wayhunter.Domain.Modules.TripSafety.Entities;
using Wayhunter.Domain.Modules.TripSafety.Enums;

namespace Wayhunter.Application.Modules.TripSafety.Validators;

public static class TripInviteEligibilityValidator
{
    public static void EnsureCanInvite(
        TripPlan? tripPlan,
        string? authenticatedUserId,
        string? recipientUserId,
        bool recipientIsActive,
        bool isBlockedRelationship,
        TripInvitation? existingInvitation,
        TripMember? existingMember)
    {
        // Authentication must be provided by
        // the trusted backend identity context.
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

        string sender = authenticatedUserId.Trim();

        if (!string.Equals(
            tripPlan.OwnerUserId,
            sender,
            StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException(
                "Only the trip owner can invite members.");
        }

        if (tripPlan.DraftStatus == TripDraftStatus.Cancelled)
        {
            throw new InvalidOperationException(
                "Cancelled trips cannot send invitations.");
        }

        if (string.IsNullOrWhiteSpace(recipientUserId))
        {
            throw new ArgumentException(
                "Recipient User ID is required.",
                nameof(recipientUserId));
        }

        string recipient = recipientUserId.Trim();

        if (string.Equals(
            sender,
            recipient,
            StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Trip owners cannot invite themselves.");
        }

        if (!recipientIsActive)
        {
            throw new InvalidOperationException(
                "Recipient account is not eligible.");
        }

        if (isBlockedRelationship)
        {
            throw new UnauthorizedAccessException(
                "Blocked users cannot receive invitations.");
        }

        if (existingMember is not null &&
            existingMember.TripPlanId == tripPlan.Id &&
            string.Equals(
                existingMember.MemberUserId,
                recipient,
                StringComparison.Ordinal) &&
            existingMember.IsActive)
        {
            throw new InvalidOperationException(
                "Recipient is already a trip member.");
        }

        if (existingInvitation is not null &&
            existingInvitation.TripPlanId == tripPlan.Id &&
            string.Equals(
                existingInvitation.RecipientUserId,
                recipient,
                StringComparison.Ordinal) &&
            (existingInvitation.Status ==
                TripInvitationStatus.Pending ||
             existingInvitation.Status ==
                TripInvitationStatus.Accepted))
        {
            throw new InvalidOperationException(
                "An open or accepted invitation already exists.");
        }

        // Eligibility verified.
        // This method does not save or send an invitation.
        // It never grants GPS sharing consent.
    }
}
