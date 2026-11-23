using Wayhunter.Application.Modules.TripSafety.Interfaces;

namespace Wayhunter.UnitTests.Modules.TripSafety;

internal sealed class TripInviteBlockReaderStub(bool blocked = false)
    : ITripInviteBlockRelationshipReader
{
    public Task<bool> IsBlockedEitherWayAsync(
        string senderUserId,
        string recipientUserId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(blocked);
    }
}
