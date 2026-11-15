
using System;
using Wayhunter.Domain.Modules.TripSafety.Enums;

namespace Wayhunter.Domain.Modules.TripSafety.Entities;

public sealed class TripMember
{
    public Guid Id { get; }

    public Guid TripPlanId { get; }

    public string MemberUserId { get; }

    public Guid InvitationId { get; }

    public DateTimeOffset JoinedAtUtc { get; }

    public bool IsActive { get; private set; }

    public DateTimeOffset? RemovedAtUtc { get; private set; }

    private TripMember(
        Guid id,
        Guid tripPlanId,
        string memberUserId,
        Guid invitationId,
        DateTimeOffset joinedAtUtc)
    {
        Id = id;
        TripPlanId = tripPlanId;
        MemberUserId = memberUserId;
        InvitationId = invitationId;
        JoinedAtUtc = joinedAtUtc;

        IsActive = true;
        RemovedAtUtc = null;
    }

    public static TripMember FromAcceptedInvitation(
        Guid memberId,
        TripInvitation invitation)
    {
        if (memberId == Guid.Empty)
        {
            throw new ArgumentException(
                "Member ID is required.",
                nameof(memberId));
        }

        ArgumentNullException.ThrowIfNull(invitation);

        if (invitation.Status != TripInvitationStatus.Accepted ||
            !invitation.RespondedAtUtc.HasValue)
        {
            throw new InvalidOperationException(
                "Only accepted invitations create membership.");
        }

        // Membership never grants automatic GPS consent.
        return new TripMember(
            memberId,
            invitation.TripPlanId,
            invitation.RecipientUserId,
            invitation.Id,
            invitation.RespondedAtUtc.Value);
    }

    // This domain operation must only be called
    // after authorization by the application service.
    //
    // Removing a member must revoke private trip access.
    // Existing historical membership is retained.

    public bool Remove(DateTimeOffset serverTimeUtc)
    {
        if (serverTimeUtc == default)
        {
            throw new ArgumentException(
                "Valid server time is required.",
                nameof(serverTimeUtc));
        }

        var removedAtUtc = serverTimeUtc.ToUniversalTime();

        // Repeated removal is idempotent.
        if (!IsActive)
        {
            return false;
        }

        if (removedAtUtc < JoinedAtUtc)
        {
            throw new InvalidOperationException(
                "Removal time cannot precede joining time.");
        }

        IsActive = false;
        RemovedAtUtc = removedAtUtc;

        return true;
    }

    public bool CanAccessPrivateTrip()
    {
        return IsActive;
    }
}
