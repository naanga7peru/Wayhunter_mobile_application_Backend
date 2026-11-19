
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Wayhunter.Application.Modules.TripSafety.Interfaces;

public sealed record TripInviteRecipientEligibility(
    bool RecipientIsActive,
    bool IsBlocked);

public interface ITripInviteEligibilityAccess
{
    Task<TripInviteRecipientEligibility> CheckAsync(
        string senderUserId,
        string recipientUserId,
        CancellationToken cancellationToken = default);
}

// M1 owner must provide the authoritative implementation.
// It must check blocking in BOTH directions.
// Never trust blocked-state flags from a mobile request.
public interface ITripInviteBlockRelationshipReader
{
    Task<bool> IsBlockedEitherWayAsync(
        string senderUserId,
        string recipientUserId,
        CancellationToken cancellationToken = default);
}

public sealed class TripInvitePrerequisiteUnavailableException
    : Exception
{
    public TripInvitePrerequisiteUnavailableException(
        string message)
        : base(message)
    {
    }
}
