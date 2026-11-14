
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
    }

    public static TripMember FromAcceptedInvitation(
        Guid memberId,
        TripInvitation invitation)
    {
        if (memberId == Guid.Empty)
            throw new ArgumentException(
                "Member ID is required.");

        if (invitation is null)
            throw new ArgumentNullException(
                nameof(invitation));

        if (invitation.Status != TripInvitationStatus.Accepted ||
            !invitation.RespondedAtUtc.HasValue)
        {
            throw new InvalidOperationException(
                "Only accepted invitations create membership.");
        }

        // Membership does not grant GPS sharing consent.
        return new TripMember(
            memberId,
            invitation.TripPlanId,
            invitation.RecipientUserId,
            invitation.Id,
            invitation.RespondedAtUtc.Value);
    }
}
