
using System;

namespace Wayhunter.Application.Modules.TripSafety.Dtos;

public sealed record TripMemberDto
{
    public Guid TripPlanId { get; }

    public string MemberUserId { get; }

    public string MembershipStatus { get; }

    public string InvitationReference { get; }

    public DateTimeOffset? AcceptedAtUtc { get; }

    public Guid? SessionId { get; }

    private TripMemberDto(
        Guid tripPlanId,
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
        Guid tripPlanId,
        string memberUserId,
        string membershipStatus,
        string invitationReference,
        DateTimeOffset? acceptedAtUtc,
        Guid? sessionId)
    {
        if (tripPlanId == Guid.Empty)
        {
            throw new ArgumentException(
                "Valid Trip Plan ID is required.",
                nameof(tripPlanId));
        }

        if (string.IsNullOrWhiteSpace(memberUserId))
        {
            throw new ArgumentException(
                "Member User ID is required.",
                nameof(memberUserId));
        }

        if (string.IsNullOrWhiteSpace(membershipStatus))
        {
            throw new ArgumentException(
                "Membership status is required.",
                nameof(membershipStatus));
        }

        if (string.IsNullOrWhiteSpace(invitationReference))
        {
            throw new ArgumentException(
                "Invitation reference is required.",
                nameof(invitationReference));
        }

        if (sessionId == Guid.Empty)
        {
            throw new ArgumentException(
                "Session ID cannot be empty.",
                nameof(sessionId));
        }

        if (acceptedAtUtc.HasValue &&
            acceptedAtUtc.Value == default)
        {
            throw new ArgumentException(
                "Invalid acceptance time.",
                nameof(acceptedAtUtc));
        }

        return new TripMemberDto(
            tripPlanId,
            memberUserId.Trim(),
            membershipStatus.Trim(),
            invitationReference.Trim(),
            acceptedAtUtc?.ToUniversalTime(),
            sessionId);
    }
}
