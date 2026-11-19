
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;

using Wayhunter.Infrastructure.Persistence.DbContexts;
using Wayhunter.Infrastructure.Persistence.Models.TripSafety;

namespace Wayhunter.Infrastructure.Persistence.Repositories.TripSafety;

public enum M3OutboxDispatchOutcome
{
    NoWork = 0,
    Delivered = 1,
    RetryScheduled = 2,
    DeadLettered = 3
}

public sealed record M3OutboxStatus(
    int Ready,
    int Delayed,
    int Leased,
    int DeadLettered,
    int Delivered,
    int Exhausted);

public sealed class M3NotificationOutboxDispatcher
{
    private readonly WayhunterSharedDbContext _db;

    private readonly ITripMembershipRevocationNotificationSender
        _sender;

    private readonly TimeProvider _clock;

    private readonly M3OutboxWorkerSettings _settings;

    public M3NotificationOutboxDispatcher(
        WayhunterSharedDbContext db,
        ITripMembershipRevocationNotificationSender sender,
        TimeProvider clock,
        M3OutboxWorkerSettings settings)
    {
        _db = db ??
            throw new ArgumentNullException(nameof(db));

        _sender = sender ??
            throw new ArgumentNullException(nameof(sender));

        _clock = clock ??
            throw new ArgumentNullException(nameof(clock));

        _settings = settings ??
            throw new ArgumentNullException(nameof(settings));

        _settings.Validate();
    }

    public async Task<M3OutboxDispatchOutcome> ProcessNextAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var claimed = await ClaimNextAsync(cancellationToken);

        if (claimed is null)
        {
            return M3OutboxDispatchOutcome.NoWork;
        }

        var notification =
            new TripMembershipRevocationNotification(
                claimed.Id,
                claimed.TripPlanId,
                claimed.MemberId,
                claimed.MemberUserId,
                claimed.RemovedAtUtc);

        try
        {
            // Sender must use notification.Id as its
            // stable idempotency key. SQL lease alone
            // cannot prevent duplicates after crashes.
            await _sender.SendAsync(
                notification,
                cancellationToken);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            // Lease expires if the worker shuts down.
            // Avoid using a cancelled token to update SQL.
            throw;
        }
        catch (Exception exception)
        {
            // Do not store exception.Message:
            // it may include private URL or provider data.
            await RecordFailureAsync(
                claimed,
                exception.GetType().Name,
                cancellationToken);

            return claimed.DeliveryAttempts >= _settings.MaxAttempts
                ? M3OutboxDispatchOutcome.DeadLettered
                : M3OutboxDispatchOutcome.RetryScheduled;
        }

        // Only a successful, durable transport
        // acknowledgement permits IsDelivered=true.
        await CompleteAsync(
            claimed,
            cancellationToken);

        return M3OutboxDispatchOutcome.Delivered;
    }

    private async Task<TripMembershipRevocationOutboxRow?>
        ClaimNextAsync(
            CancellationToken cancellationToken)
    {
        var now = _clock.GetUtcNow().ToUniversalTime();

        var candidates = await _db.TripMembershipRevocationOutbox
            .AsNoTracking()
            .Where(x =>
                !x.IsDelivered &&
                x.DeadLetteredAtUtc == null &&
                x.DeliveryAttempts < _settings.MaxAttempts &&
                (x.NextAttemptAtUtc == null ||
                 x.NextAttemptAtUtc <= now) &&
                (x.LeaseUntilUtc == null ||
                 x.LeaseUntilUtc <= now))
            .OrderBy(x => x.CreatedAtUtc)
            .ThenBy(x => x.Id)
            .Take(10)
            .ToListAsync(cancellationToken);

        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var token = Guid.NewGuid();

            var leaseUntil =
                now.AddSeconds(_settings.LeaseSeconds);

            if (_db.Database.IsRelational())
            {
                // Atomic compare-and-claim.
                //
                // Multiple SQL workers cannot claim the
                // same event while the lease is valid.

                var updated =
                    await _db.TripMembershipRevocationOutbox
                        .Where(x =>
                            x.Id == candidate.Id &&
                            !x.IsDelivered &&
                            x.DeadLetteredAtUtc == null &&
                            x.DeliveryAttempts <
                                _settings.MaxAttempts &&
                            (x.NextAttemptAtUtc == null ||
                             x.NextAttemptAtUtc <= now) &&
                            (x.LeaseUntilUtc == null ||
                             x.LeaseUntilUtc <= now))
                        .ExecuteUpdateAsync(
                            update => update
                                .SetProperty(
                                    x => x.LeaseToken,
                                    (Guid?)token)
                                .SetProperty(
                                    x => x.LeaseUntilUtc,
                                    (DateTimeOffset?)leaseUntil)
                                .SetProperty(
                                    x => x.DeliveryAttempts,
                                    x => x.DeliveryAttempts + 1),
                            cancellationToken);

                if (updated != 1)
                {
                    // Another worker won the claim.
                    continue;
                }

                candidate.LeaseToken = token;
                candidate.LeaseUntilUtc = leaseUntil;
                candidate.DeliveryAttempts++;

                return candidate;
            }

            // EF InMemory fallback for unit tests only.
            // This path does not provide distributed
            // lease guarantees and must not be used
            // for production workers.

            var tracked =
                await _db.TripMembershipRevocationOutbox
                    .SingleOrDefaultAsync(
                        x => x.Id == candidate.Id,
                        cancellationToken);

            if (tracked is null ||
                tracked.IsDelivered ||
                tracked.DeadLetteredAtUtc.HasValue ||
                tracked.DeliveryAttempts >= _settings.MaxAttempts ||
                (tracked.NextAttemptAtUtc.HasValue &&
                 tracked.NextAttemptAtUtc.Value > now) ||
                (tracked.LeaseUntilUtc.HasValue &&
                 tracked.LeaseUntilUtc.Value > now))
            {
                continue;
            }

            tracked.LeaseToken = token;
            tracked.LeaseUntilUtc = leaseUntil;

            tracked.DeliveryAttempts =
                checked(tracked.DeliveryAttempts + 1);

            await _db.SaveChangesAsync(cancellationToken);

            return tracked;
        }

        return null;
    }

    private async Task CompleteAsync(
        TripMembershipRevocationOutboxRow claimed,
        CancellationToken cancellationToken)
    {
        var now = _clock.GetUtcNow().ToUniversalTime();

        if (_db.Database.IsRelational())
        {
            var updated =
                await _db.TripMembershipRevocationOutbox
                    .Where(x =>
                        x.Id == claimed.Id &&
                        !x.IsDelivered &&
                        x.LeaseToken == claimed.LeaseToken)
                    .ExecuteUpdateAsync(
                        update => update
                            .SetProperty(
                                x => x.IsDelivered,
                                true)
                            .SetProperty(
                                x => x.DeliveredAtUtc,
                                (DateTimeOffset?)now)
                            .SetProperty(
                                x => x.LeaseToken,
                                (Guid?)null)
                            .SetProperty(
                                x => x.LeaseUntilUtc,
                                (DateTimeOffset?)null)
                            .SetProperty(
                                x => x.NextAttemptAtUtc,
                                (DateTimeOffset?)null)
                            .SetProperty(
                                x => x.LastError,
                                (string?)null),
                        cancellationToken);

            if (updated != 1)
            {
                throw new InvalidOperationException(
                    "M3 notification delivery lease was lost.");
            }

            return;
        }

        var tracked = await GetTrackedClaimAsync(
            claimed,
            cancellationToken);

        tracked.IsDelivered = true;
        tracked.DeliveredAtUtc = now;
        tracked.LeaseToken = null;
        tracked.LeaseUntilUtc = null;
        tracked.NextAttemptAtUtc = null;
        tracked.LastError = null;

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task RecordFailureAsync(
        TripMembershipRevocationOutboxRow claimed,
        string errorType,
        CancellationToken cancellationToken)
    {
        var now = _clock.GetUtcNow().ToUniversalTime();

        var deadLetter =
            claimed.DeliveryAttempts >= _settings.MaxAttempts;

        DateTimeOffset? retryAt = deadLetter
            ? null
            : now.Add(M3OutboxRetryPolicy.CalculateDelay(
                claimed.DeliveryAttempts,
                _settings.BaseRetrySeconds,
                _settings.MaxRetrySeconds));

        DateTimeOffset? deadLetteredAt = deadLetter
            ? now
            : null;

        var safeError = errorType.Length > 256
            ? errorType[..256]
            : errorType;

        if (_db.Database.IsRelational())
        {
            var updated =
                await _db.TripMembershipRevocationOutbox
                    .Where(x =>
                        x.Id == claimed.Id &&
                        !x.IsDelivered &&
                        x.LeaseToken == claimed.LeaseToken)
                    .ExecuteUpdateAsync(
                        update => update
                            .SetProperty(
                                x => x.NextAttemptAtUtc,
                                retryAt)
                            .SetProperty(
                                x => x.DeadLetteredAtUtc,
                                deadLetteredAt)
                            .SetProperty(
                                x => x.LastError,
                                safeError)
                            .SetProperty(
                                x => x.LeaseToken,
                                (Guid?)null)
                            .SetProperty(
                                x => x.LeaseUntilUtc,
                                (DateTimeOffset?)null),
                        cancellationToken);

            if (updated != 1)
            {
                throw new InvalidOperationException(
                    "M3 notification failure lease was lost.");
            }

            return;
        }

        var tracked = await GetTrackedClaimAsync(
            claimed,
            cancellationToken);

        tracked.NextAttemptAtUtc = retryAt;
        tracked.DeadLetteredAtUtc = deadLetteredAt;
        tracked.LastError = safeError;
        tracked.LeaseToken = null;
        tracked.LeaseUntilUtc = null;

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<TripMembershipRevocationOutboxRow>
        GetTrackedClaimAsync(
            TripMembershipRevocationOutboxRow claimed,
            CancellationToken cancellationToken)
    {
        var tracked =
            await _db.TripMembershipRevocationOutbox
                .SingleOrDefaultAsync(
                    x => x.Id == claimed.Id,
                    cancellationToken);

        if (tracked is null ||
            tracked.IsDelivered ||
            tracked.LeaseToken != claimed.LeaseToken)
        {
            throw new InvalidOperationException(
                "M3 notification lease no longer belongs to worker.");
        }

        return tracked;
    }

    // Operational counts only.
    // Never return member identities or notification payloads.

    public async Task<M3OutboxStatus> GetStatusAsync(
        CancellationToken cancellationToken = default)
    {
        var now = _clock.GetUtcNow().ToUniversalTime();

        var query = _db.TripMembershipRevocationOutbox
            .AsNoTracking();

        var ready = await query.CountAsync(
            x =>
                !x.IsDelivered &&
                x.DeadLetteredAtUtc == null &&
                x.DeliveryAttempts < _settings.MaxAttempts &&
                (x.NextAttemptAtUtc == null ||
                 x.NextAttemptAtUtc <= now) &&
                (x.LeaseUntilUtc == null ||
                 x.LeaseUntilUtc <= now),
            cancellationToken);

        var delayed = await query.CountAsync(
            x =>
                !x.IsDelivered &&
                x.DeadLetteredAtUtc == null &&
                x.NextAttemptAtUtc > now,
            cancellationToken);

        var leased = await query.CountAsync(
            x =>
                !x.IsDelivered &&
                x.DeadLetteredAtUtc == null &&
                x.LeaseUntilUtc > now,
            cancellationToken);

        var dead = await query.CountAsync(
            x => x.DeadLetteredAtUtc != null,
            cancellationToken);

        var delivered = await query.CountAsync(
            x => x.IsDelivered,
            cancellationToken);

        var exhausted = await query.CountAsync(
            x =>
                !x.IsDelivered &&
                x.DeadLetteredAtUtc == null &&
                x.DeliveryAttempts >= _settings.MaxAttempts,
            cancellationToken);

        return new M3OutboxStatus(
            ready,
            delayed,
            leased,
            dead,
            delivered,
            exhausted);
    }
}
