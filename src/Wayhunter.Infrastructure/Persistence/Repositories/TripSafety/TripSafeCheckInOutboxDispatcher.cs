using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Wayhunter.Application.Modules.TripSafety.Interfaces;
using Wayhunter.Domain.Modules.IdentitySocial.Enums;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Wayhunter.Infrastructure.Persistence.Models.TripSafety;

namespace Wayhunter.Infrastructure.Persistence.Repositories.TripSafety;

public enum TripSafeCheckInDispatchOutcome
{
    NoWork,
    Delivered,
    RetryScheduled,
    DeadLettered,
    LeaseLost
}

public sealed class TripSafeCheckInOutboxDispatcher
{
    private readonly WayhunterSharedDbContext _db;
    private readonly ITripSafeCheckInOwnerNotificationSender _sender;
    private readonly TimeProvider _clock;
    private readonly M3OutboxWorkerSettings _settings;

    public TripSafeCheckInOutboxDispatcher(WayhunterSharedDbContext db,
        ITripSafeCheckInOwnerNotificationSender sender, TimeProvider clock, M3OutboxWorkerSettings settings)
    {
        if (!db.Database.IsSqlServer())
            throw new InvalidOperationException("Owner notification dispatch requires SQL Server.");

        settings.Validate();
        _db = db;
        _sender = sender;
        _clock = clock;
        _settings = settings;
    }

    public async Task<TripSafeCheckInDispatchOutcome> ProcessNextAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var now = _clock.GetUtcNow();
        var pending = _db.TripSafeCheckInOutbox.Where(x => !x.IsDelivered && x.DeadLetteredAtUtc == null);
        await pending.Where(x => x.DeliveryAttempts >= _settings.MaxAttempts &&
                (x.LeaseUntilUtc == null || x.LeaseUntilUtc <= now))
            .ExecuteUpdateAsync(set => set
                .SetProperty(x => x.DeadLetteredAtUtc, now)
                .SetProperty(x => x.LeaseToken, (Guid?)null)
                .SetProperty(x => x.LeaseUntilUtc, (DateTimeOffset?)null)
                .SetProperty(x => x.NextAttemptAtUtc, (DateTimeOffset?)null)
                .SetProperty(x => x.LastError, "AttemptLimitExhausted"), cancellationToken);

        var ready = pending.Where(x => x.DeliveryAttempts < _settings.MaxAttempts &&
            (x.NextAttemptAtUtc == null || x.NextAttemptAtUtc <= now) &&
            (x.LeaseUntilUtc == null || x.LeaseUntilUtc <= now));

        var id = await ready.OrderBy(x => x.NextAttemptAtUtc).ThenBy(x => x.CreatedAtUtc).ThenBy(x => x.Id)
            .Select(x => (Guid?)x.Id).FirstOrDefaultAsync(cancellationToken);
        if (id is null)
            return TripSafeCheckInDispatchOutcome.NoWork;

        var token = Guid.NewGuid();
        var expiry = now.AddSeconds(_settings.LeaseSeconds);
        var updated = await ready.Where(x => x.Id == id).ExecuteUpdateAsync(set => set
            .SetProperty(x => x.LeaseToken, token)
            .SetProperty(x => x.LeaseUntilUtc, expiry)
            .SetProperty(x => x.DeliveryAttempts, x => x.DeliveryAttempts + 1), cancellationToken);
        if (updated != 1)
            return TripSafeCheckInDispatchOutcome.LeaseLost;

        var claim = pending.Where(x => x.Id == id && x.LeaseToken == token);
        var attempt = await claim.Select(x => (int?)x.DeliveryAttempts).SingleOrDefaultAsync(cancellationToken);
        if (attempt is null)
            return TripSafeCheckInDispatchOutcome.LeaseLost;

        TripSafeCheckInOwnerNotification? notification;
        try
        {
            notification = await FindAuthorizedNotificationAsync(claim, cancellationToken);
        }
        catch (Exception error) when (error is not OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            return await RecordFailureAsync(claim, attempt.Value, "RecipientVerificationFailed", false, cancellationToken);
        }

        if (notification is null)
            return await RecordFailureAsync(claim, attempt.Value, "RecipientNotAuthorized", true, cancellationToken);

        var remaining = expiry - _clock.GetUtcNow();
        if (remaining <= TimeSpan.Zero)
            return TripSafeCheckInDispatchOutcome.LeaseLost;

        // A sender must observe cancellation. An external send and SQL acknowledgement
        // cannot be atomic, so redelivery always carries the original event ID.
        using var deadline = new CancellationTokenSource(remaining, _clock);
        using var delivery = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        try
        {
            delivery.Token.ThrowIfCancellationRequested();
            await _sender.SendAsync(notification, delivery.Token);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return await RecordFailureAsync(claim, attempt.Value, "DeliveryFailed", false, cancellationToken);
        }

        var deliveredAt = _clock.GetUtcNow();
        updated = await Unexpired(claim, deliveredAt).ExecuteUpdateAsync(set => set
            .SetProperty(x => x.IsDelivered, true)
            .SetProperty(x => x.DeliveredAtUtc, deliveredAt)
            .SetProperty(x => x.LeaseToken, (Guid?)null)
            .SetProperty(x => x.LeaseUntilUtc, (DateTimeOffset?)null)
            .SetProperty(x => x.NextAttemptAtUtc, (DateTimeOffset?)null)
            .SetProperty(x => x.LastError, (string?)null), cancellationToken);
        return updated == 1 ? TripSafeCheckInDispatchOutcome.Delivered : TripSafeCheckInDispatchOutcome.LeaseLost;
    }

    private async Task<TripSafeCheckInOwnerNotification?> FindAuthorizedNotificationAsync(
        IQueryable<TripSafeCheckInOutboxRow> claim, CancellationToken cancellationToken)
    {
        var candidate = await (
            from row in Unexpired(claim, _clock.GetUtcNow())
            join checkIn in _db.TripSafeCheckIns on row.CheckInId equals checkIn.CheckInId
            join trip in _db.Trips on checkIn.TripPlanId equals trip.Id
            join recipient in _db.Users on row.RecipientUserId equals recipient.UserId
            where recipient.Status == UserAccountStatus.Active && recipient.VerifiedAtUtc != null &&
                recipient.DeletionRequestedAtUtc == null
            select new
            {
                trip.OwnerUserId,
                recipient.UserId,
                Notification = new TripSafeCheckInOwnerNotification(row.Id, row.RecipientUserId, checkIn.CheckInId,
                    checkIn.TripPlanId, checkIn.SessionId, checkIn.UserId, checkIn.CheckedInAtUtc)
            }).SingleOrDefaultAsync(cancellationToken);

        return candidate is not null &&
            string.Equals(candidate.Notification.RecipientUserId, candidate.OwnerUserId, StringComparison.Ordinal) &&
            string.Equals(candidate.Notification.RecipientUserId, candidate.UserId, StringComparison.Ordinal)
            ? candidate.Notification : null;
    }

    private async Task<TripSafeCheckInDispatchOutcome> RecordFailureAsync(
        IQueryable<TripSafeCheckInOutboxRow> claim, int attempt, string classification, bool terminal,
        CancellationToken cancellationToken)
    {
        var now = _clock.GetUtcNow();
        var dead = terminal || attempt >= _settings.MaxAttempts;
        DateTimeOffset? retry = dead ? null : now.Add(M3OutboxRetryPolicy.CalculateDelay(
            attempt, _settings.BaseRetrySeconds, _settings.MaxRetrySeconds));
        DateTimeOffset? deadAt = dead ? now : null;
        var updated = await Unexpired(claim, now).ExecuteUpdateAsync(set => set
            .SetProperty(x => x.LeaseToken, (Guid?)null)
            .SetProperty(x => x.LeaseUntilUtc, (DateTimeOffset?)null)
            .SetProperty(x => x.NextAttemptAtUtc, retry)
            .SetProperty(x => x.DeadLetteredAtUtc, deadAt)
            .SetProperty(x => x.LastError, classification), cancellationToken);
        if (updated != 1)
            return TripSafeCheckInDispatchOutcome.LeaseLost;

        return dead ? TripSafeCheckInDispatchOutcome.DeadLettered : TripSafeCheckInDispatchOutcome.RetryScheduled;
    }

    private static IQueryable<TripSafeCheckInOutboxRow> Unexpired(
        IQueryable<TripSafeCheckInOutboxRow> claim, DateTimeOffset now) =>
        claim.Where(x => x.LeaseUntilUtc > now);
}
