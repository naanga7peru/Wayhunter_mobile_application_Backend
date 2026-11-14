
using System;

namespace Wayhunter.Application.Modules.TripSafety.Dtos;

public sealed record TripMemberDto
{
    public string TripPlanId { get; }

    public string MemberUserId { get; }

    public string MembershipStatus { get; }

    public string InvitationReference { get; }

    public DateTimeOffset? AcceptedAtUtc { get; }

    public Guid? SessionId { get; }

    private TripMemberDto(
        string tripPlanId,
        string memberUserId,
        string membershipStatus,
        string invitationReference,
        DateTimeOffset? acceptedAtUtc,
        Guid? sessionId)
    {
        TripPlanId = tripPlanId;
        MemberUserId = memberUserId;
        MembershipStatus = membershipStatus;
        InvitationReference = invitationReference;
        AcceptedAtUtc = acceptedAtUtc;
        SessionId = sessionId;
    }

    public static TripMemberDto Create(
        string tripPlanId,
        string memberUserId,
        string membershipStatus,
        string invitationReference,
        DateTimeOffset? acceptedAtUtc,
        Guid? sessionId)
    {
        if (string.IsNullOrWhiteSpace(tripPlanId))
            throw new ArgumentException(
                "Trip Plan ID is required.");

        if (string.IsNullOrWhiteSpace(memberUserId))
            throw new ArgumentException(
                "Member User ID is required.");

        if (string.IsNullOrWhiteSpace(membershipStatus))
            throw new ArgumentException(
                "Membership status is required.");

        if (string.IsNullOrWhiteSpace(invitationReference))
            throw new ArgumentException(
                "Invitation reference is required.");

        if (sessionId == Guid.Empty)
            throw new ArgumentException(
                "Session ID cannot be empty.");

        if (acceptedAtUtc.HasValue &&
            acceptedAtUtc.Value == default)
            throw new ArgumentException(
                "Invalid acceptance time.");

        return new TripMemberDto(
            tripPlanId.Trim(),
            memberUserId.Trim(),
            membershipStatus.Trim(),
            invitationReference.Trim(),
            acceptedAtUtc?.ToUniversalTime(),
            sessionId);
    }
}
