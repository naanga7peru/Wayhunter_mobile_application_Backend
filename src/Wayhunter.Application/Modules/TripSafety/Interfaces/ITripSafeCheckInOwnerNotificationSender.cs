using System;
using System.Threading;
using System.Threading.Tasks;

namespace Wayhunter.Application.Modules.TripSafety.Interfaces;

public sealed record TripSafeCheckInOwnerNotification(
    Guid Id, string RecipientUserId, Guid CheckInId, Guid TripPlanId,
    Guid SessionId, string CheckedInUserId, DateTimeOffset CheckedInAtUtc);

public interface ITripSafeCheckInOwnerNotificationSender
{
    // The transport must enforce Id as a durable consumer idempotency key.
    Task SendAsync(TripSafeCheckInOwnerNotification notification, CancellationToken cancellationToken);
}
