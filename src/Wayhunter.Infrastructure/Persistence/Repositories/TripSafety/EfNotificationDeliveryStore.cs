using System.Data;
using Microsoft.EntityFrameworkCore;
using Wayhunter.Application.Modules.TripSafety.Notifications;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Wayhunter.Infrastructure.Persistence.Models.TripSafety;

namespace Wayhunter.Infrastructure.Persistence.Repositories.TripSafety;

// SQL implementation is deliberately unregistered pending schema and provider approval.
public sealed class EfNotificationDeliveryStore(IDbContextFactory<WayhunterSharedDbContext> contexts) : INotificationDeliveryStore
{
    public async Task<NotificationLease?> ClaimAsync(DateTimeOffset now, int maxAttempts,
        TimeSpan leaseDuration, CancellationToken ct)
    {
        return await ExecuteAsync<NotificationLease?>(async db =>
        {
            var pending = db.NotificationRecipients.Where(x => x.State == NotificationDeliveryState.Pending &&
                (x.LeaseUntilUtc == null || x.LeaseUntilUtc <= now));
            var candidate = await (
                from recipient in pending
                join message in db.NotificationEvents on recipient.EventId equals message.EventId
                where recipient.Attempts >= maxAttempts || recipient.NextAttemptAtUtc == null || recipient.NextAttemptAtUtc <= now
                orderby recipient.Attempts >= maxAttempts descending, recipient.NextAttemptAtUtc, message.CreatedAtUtc,
                    recipient.EventId, recipient.UserId
                select new { recipient.EventId, recipient.UserId, recipient.Attempts, message.Category,
                    message.Template, message.TemplateVersion }).FirstOrDefaultAsync(ct);
            if (candidate is null)
                return null;

            var row = pending.Where(x => x.EventId == candidate.EventId && x.UserId == candidate.UserId &&
                x.Attempts == candidate.Attempts);
            if (candidate.Attempts >= maxAttempts)
            {
                var expired = await row.ExecuteUpdateAsync(set => set
                    .SetProperty(x => x.State, NotificationDeliveryState.DeadLettered)
                    .SetProperty(x => x.CompletedAtUtc, now)
                    .SetProperty(x => x.LeaseToken, (Guid?)null)
                    .SetProperty(x => x.LeaseUntilUtc, (DateTimeOffset?)null)
                    .SetProperty(x => x.NextAttemptAtUtc, (DateTimeOffset?)null)
                    .SetProperty(x => x.LastOutcome, "ExpiredAttemptLimit"), ct);
                if (expired == 1)
                    await AuditAsync(db, Guid.NewGuid(), candidate.EventId, candidate.UserId, candidate.Attempts,
                        "ExpiredAttemptLimit", now, ct);
                return null;
            }

            var token = Guid.NewGuid();
            var expiry = now.Add(leaseDuration);
            var updated = await row.Where(x => x.NextAttemptAtUtc == null || x.NextAttemptAtUtc <= now)
                .ExecuteUpdateAsync(set => set
                    .SetProperty(x => x.Attempts, x => x.Attempts + 1)
                    .SetProperty(x => x.LeaseToken, token)
                    .SetProperty(x => x.LeaseUntilUtc, expiry), ct);
            if (updated != 1)
                return null;

            await AuditAsync(db, Guid.NewGuid(), candidate.EventId, candidate.UserId, candidate.Attempts + 1,
                "Claimed", now, ct);
            return new NotificationLease(token, candidate.EventId, candidate.UserId, candidate.Attempts + 1,
                expiry, candidate.Category, candidate.Template, candidate.TemplateVersion);
        }, ct);
    }

    public Task<bool> FinishAsync(NotificationLease lease, NotificationAttemptResult result, DateTimeOffset now,
        CancellationToken ct) => ExecuteAsync(async db =>
    {
        DateTimeOffset? completed = result.State == NotificationDeliveryState.Pending ? null : now;
        var updated = await db.NotificationRecipients
            .Where(x => x.EventId == lease.EventId && x.UserId == lease.UserId && x.Attempts == lease.Attempt &&
                x.State == NotificationDeliveryState.Pending && x.LeaseToken == lease.Token && x.LeaseUntilUtc > now)
            .ExecuteUpdateAsync(set => set
                .SetProperty(x => x.State, result.State)
                .SetProperty(x => x.CompletedAtUtc, completed)
                .SetProperty(x => x.LeaseToken, (Guid?)null)
                .SetProperty(x => x.LeaseUntilUtc, (DateTimeOffset?)null)
                .SetProperty(x => x.NextAttemptAtUtc, result.RetryAtUtc)
                .SetProperty(x => x.LastOutcome, result.Outcome), ct);
        if (updated != 1)
            return false;

        await AuditAsync(db, lease.Token, lease.EventId, lease.UserId, lease.Attempt, result.Outcome, now, ct);
        return true;
    }, ct);

    private static async Task AuditAsync(WayhunterSharedDbContext db, Guid id, Guid eventId, string userId,
        int attempt, string outcome, DateTimeOffset now, CancellationToken ct)
    {
        db.NotificationDeliveryAudit.Add(new NotificationDeliveryAuditRow
        {
            AuditId = id, EventId = eventId, UserId = userId, Attempt = attempt, Outcome = outcome, RecordedAtUtc = now
        });
        await db.SaveChangesAsync(ct);
    }

    private async Task<T> ExecuteAsync<T>(Func<WayhunterSharedDbContext, Task<T>> action, CancellationToken ct)
    {
        await using var strategyDb = await contexts.CreateDbContextAsync(ct);
        if (!strategyDb.Database.IsSqlServer())
            throw new NotificationUnavailableException();

        return await strategyDb.Database.CreateExecutionStrategy().ExecuteAsync(async token =>
        {
            await using var db = await contexts.CreateDbContextAsync(token);
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, token);
            var result = await action(db);
            await tx.CommitAsync(token);
            return result;
        }, ct);
    }
}
