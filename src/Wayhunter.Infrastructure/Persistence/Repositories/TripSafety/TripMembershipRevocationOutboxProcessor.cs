
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;

using Wayhunter.Infrastructure.Persistence.DbContexts;
using Wayhunter.Infrastructure.Persistence.Models.TripSafety;

namespace Wayhunter.Infrastructure.Persistence.Repositories.TripSafety;

public sealed class TripMembershipRevocationOutboxProcessor
{
    private readonly WayhunterSharedDbContext _db;
    private readonly ITripMembershipRevocationNotificationSender _sender;
    private readonly TimeProvider _timeProvider;

    public TripMembershipRevocationOutboxProcessor(
        WayhunterSharedDbContext db,
        ITripMembershipRevocationNotificationSender sender,
        TimeProvider timeProvider)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));

        _sender = sender
            ?? throw new ArgumentNullException(nameof(sender));

        _timeProvider = timeProvider
            ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    // Process one oldest pending event.
    //
    // Important:
    // This foundation processor does not implement
    // distributed leasing or concurrent worker claims.
    // Do not run multiple dispatchers concurrently.

    public async Task<bool> ProcessNextAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var row = await _db.TripMembershipRevocationOutbox
            .Where(x => !x.IsDelivered)
            .OrderBy(x => x.CreatedAtUtc)
            .ThenBy(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (row is null)
        {
            return false;
        }

        var notification =
            new TripMembershipRevocationNotification(
                row.Id,
                row.TripPlanId,
                row.MemberId,
                row.MemberUserId,
                row.RemovedAtUtc);

        // Record a delivery attempt before sending.
        // A crash after this save may leave the
        // event pending for a later retry.
        row.DeliveryAttempts =
            checked(row.DeliveryAttempts + 1);

        await _db.SaveChangesAsync(cancellationToken);

        // If delivery fails, the event stays pending.
        // The sender must deduplicate by NotificationId.
        await _sender.SendAsync(
            notification,
            cancellationToken);

        // Mark delivered only after sender success.
        row.IsDelivered = true;
        row.DeliveredAtUtc =
            _timeProvider.GetUtcNow().ToUniversalTime();

        await _db.SaveChangesAsync(cancellationToken);

        return true;
    }
}
