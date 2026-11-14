
using System;
using Wayhunter.Domain.Modules.TripSafety.Enums;

namespace Wayhunter.Domain.Modules.TripSafety.Entities;

public sealed class TripInvitation
{
    public Guid Id { get; }

    public Guid TripPlanId { get; }

    public string SenderUserId { get; }

    public string RecipientUserId { get; }

    public TripInvitationStatus Status { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; }

    public DateTimeOffset? RespondedAtUtc { get; private set; }

    private TripInvitation(
        Guid id,
        Guid tripPlanId,
        string senderUserId,
        string recipientUserId,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        TripPlanId = tripPlanId;
        SenderUserId = senderUserId;
        RecipientUserId = recipientUserId;
        CreatedAtUtc = createdAtUtc;
        Status = TripInvitationStatus.Pending;
    }

    public static TripInvitation Create(
        Guid invitationId,
        Guid tripPlanId,
        string senderUserId,
        string recipientUserId,
        DateTimeOffset serverTimeUtc)
    {
        if (invitationId == Guid.Empty)
            throw new ArgumentException(
                "Invitation ID is required.");

        if (tripPlanId == Guid.Empty)
            throw new ArgumentException(
                "Trip Plan ID is required.");

        if (string.IsNullOrWhiteSpace(senderUserId))
            throw new ArgumentException(
                "Sender User ID is required.");

        if (string.IsNullOrWhiteSpace(recipientUserId))
            throw new ArgumentException(
                "Recipient User ID is required.");

        var sender = senderUserId.Trim();
        var recipient = recipientUserId.Trim();

        if (string.Equals(
            sender,
            recipient,
            StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Users cannot invite themselves.");
        }

        var createdAtUtc = ValidateTime(serverTimeUtc);

        return new TripInvitation(
            invitationId,
            tripPlanId,
            sender,
            recipient,
            createdAtUtc);
    }

    public bool Accept(DateTimeOffset serverTimeUtc)
    {
        return ChangeStatus(
            TripInvitationStatus.Accepted,
            serverTimeUtc);
    }

    public bool Reject(DateTimeOffset serverTimeUtc)
    {
        return ChangeStatus(
            TripInvitationStatus.Rejected,
            serverTimeUtc);
    }

    public bool Cancel(DateTimeOffset serverTimeUtc)
    {
        return ChangeStatus(
            TripInvitationStatus.Cancelled,
            serverTimeUtc);
    }

    private bool ChangeStatus(
        TripInvitationStatus newStatus,
        DateTimeOffset serverTimeUtc)
    {
        var timestamp = ValidateTime(serverTimeUtc);

        if (Status == newStatus)
            return false;

        if (Status != TripInvitationStatus.Pending)
        {
            throw new InvalidOperationException(
                "Only pending invitations can change status.");
        }

        if (timestamp < CreatedAtUtc)
        {
            throw new InvalidOperationException(
                "Response cannot precede invitation creation.");
        }

        Status = newStatus;
        RespondedAtUtc = timestamp;

        return true;
    }

    private static DateTimeOffset ValidateTime(
        DateTimeOffset serverTimeUtc)
    {
        if (serverTimeUtc == default)
            throw new ArgumentException(
                "Valid server time is required.");

        return serverTimeUtc.ToUniversalTime();
    }
}
