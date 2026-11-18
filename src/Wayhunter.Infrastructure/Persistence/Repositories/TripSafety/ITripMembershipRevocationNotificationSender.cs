
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Wayhunter.Infrastructure.Persistence.Repositories.TripSafety;

public sealed record TripMembershipRevocationNotification(
    Guid NotificationId,
    Guid TripPlanId,
    Guid MemberId,
    string MemberUserId,
    DateTimeOffset RemovedAtUtc);

public interface ITripMembershipRevocationNotificationSender
{
    // The implementation must provide idempotent,
    // durable delivery acknowledgement.
    //
    // A successful return must mean the transport
    // accepted the notification durably.
    //
    // Use NotificationId as the idempotency key.

    Task SendAsync(
        TripMembershipRevocationNotification notification,
        CancellationToken cancellationToken = default);
}
