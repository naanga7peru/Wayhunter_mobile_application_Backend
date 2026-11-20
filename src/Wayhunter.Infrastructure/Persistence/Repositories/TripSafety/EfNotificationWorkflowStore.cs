using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Wayhunter.Application.Modules.TripSafety.Notifications;
using Wayhunter.Domain.Modules.IdentitySocial.Enums;
using Wayhunter.Domain.Modules.TravelPlanning.Enums;
using Wayhunter.Domain.Modules.TripSafety.Enums;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Wayhunter.Infrastructure.Persistence.Models.TripSafety;

namespace Wayhunter.Infrastructure.Persistence.Repositories.TripSafety;

// Proposed SQL implementation. This store is not registered in production.
public sealed class EfNotificationWorkflowStore(IDbContextFactory<WayhunterSharedDbContext> contexts, TimeProvider clock)
    : INotificationInbox, INotificationJobs, INotificationReminderPublisher
{
    public async Task<NotificationInboxPage> ReadAsync(string recipientId, int limit, CancellationToken ct)
    {
        await using var db = await OpenAsync(ct);
        var owned = db.NotificationRecipients.AsNoTracking().Where(x => x.UserId == recipientId);
        var unread = await owned.LongCountAsync(x => x.ReadAtUtc == null, ct);
        var items = await (
            from row in owned
            join message in db.NotificationEvents on row.EventId equals message.EventId
            orderby message.CreatedAtUtc descending, message.EventId
            select new NotificationInboxItem(message.EventId, message.Category, message.Template,
                message.TemplateVersion, message.TargetId.HasValue
                    ? new NotificationTarget(message.Category, message.TargetId.Value) : null,
                message.CreatedAtUtc, row.ReadAtUtc)).Take(limit).ToListAsync(ct);
        return new(items, unread);
    }

    public Task<bool> MarkReadAsync(string recipientId, Guid eventId, DateTimeOffset now, CancellationToken ct) =>
        WriteAsync(async db => await db.NotificationRecipients.Where(x => x.UserId == recipientId && x.EventId == eventId)
            .ExecuteUpdateAsync(set => set.SetProperty(x => x.ReadAtUtc, x => x.ReadAtUtc ?? now), ct) == 1, ct);

    public async Task<IReadOnlyList<NotificationPreference>> GetPreferencesAsync(string recipientId, CancellationToken ct)
    {
        await using var db = await OpenAsync(ct);
        var choices = await db.NotificationPreferences.AsNoTracking().Where(x => x.UserId == recipientId)
            .ToDictionaryAsync(x => x.Category, x => x.PushEnabled, ct);
        return Enum.GetValues<NotificationCategory>()
            .Select(category => new NotificationPreference(category, choices.GetValueOrDefault(category))).ToArray();
    }

    public Task SetPreferenceAsync(string recipientId, NotificationPreference preference, CancellationToken ct) =>
        WriteAsync(async db =>
        {
            var record = await db.NotificationPreferences.FindAsync([recipientId, preference.Category], ct);
            if (record is null)
            {
                record = new NotificationPreferenceRow { UserId = recipientId, Category = preference.Category };
                db.NotificationPreferences.Add(record);
            }
            record.PushEnabled = preference.PushEnabled;
            return true;
        }, ct);

    public Task SavePreviewAsync(NotificationAudienceReceipt receipt, CancellationToken ct) =>
        WriteAsync(async db =>
        {
            if (!await db.NotificationAudienceReceipts.AnyAsync(x => x.ReceiptId == receipt.ReceiptId, ct))
                db.NotificationAudienceReceipts.Add(new NotificationAudienceReceiptRow
                {
                    ReceiptId = receipt.ReceiptId, ActorId = receipt.ActorId, Fingerprint = receipt.Fingerprint,
                    Count = receipt.Count, ExpiresAtUtc = receipt.ExpiresAtUtc
                });
            return true;
        }, ct);

    public Task<NotificationJobReceipt> ScheduleAdminAsync(NotificationAdminSchedule input, CancellationToken ct) =>
        WriteAsync(async db =>
        {
            const string action = "AdminSchedule";
            var command = input.Command;
            var replay = await FindOperationAsync(db, input.ActorId, command.OperationId, action, input.Fingerprint, ct);
            if (replay is not null)
                return Receipt(replay);
            if (command.SendAtUtc is { } at && at <= input.Now)
                throw new NotificationConflictException();

            if (input.Recipients.Count > 1)
            {
                var preview = await db.NotificationAudienceReceipts.SingleOrDefaultAsync(x =>
                    x.ReceiptId == input.PreviewReceiptId && x.ActorId == input.ActorId && x.ConsumedAtUtc == null &&
                    x.ExpiresAtUtc > input.Now && x.Count == input.Recipients.Count, ct);
                if (preview is null || preview.Fingerprint != input.Fingerprint)
                    throw new NotificationConflictException();
                preview.ConsumedAtUtc = input.Now;
            }

            var template = NotificationTemplates.Get(command.Template);
            var job = new NotificationJobRow
            {
                JobId = Guid.NewGuid(), ActorId = input.ActorId, Kind = NotificationJobKind.AdminMessage,
                DueAtUtc = command.SendAtUtc ?? input.Now, Revision = 1, Template = command.Template,
                TemplateVersion = template.Version, TargetId = command.Target?.Id,
                AudienceRole = command.Audience.Role, AudienceDistrict = command.Audience.District,
                AudienceFingerprint = input.Fingerprint
            };
            db.NotificationJobs.Add(job);
            db.NotificationJobRecipients.AddRange(input.Recipients.Select(id =>
                new NotificationJobRecipientRow { JobId = job.JobId, UserId = id }));
            Record(db, job, command.OperationId, action, input.Fingerprint, input.Now);
            return Receipt(job);
        }, ct);

    public Task<NotificationJobReceipt> ScheduleReminderAsync(NotificationReminder input, CancellationToken ct) =>
        WriteAsync(async db =>
        {
            const string action = "ReminderSchedule";
            var command = input.Command;
            var fingerprint = Fingerprint(input);
            if (!await IsCurrentTripAsync(db, input, ct))
                throw new NotificationConflictException();

            var replay = await FindOperationAsync(db, input.UserId, command.OperationId, action, fingerprint, ct);
            if (replay is not null)
                return Receipt(replay);
            var now = clock.GetUtcNow();
            if (command.DueAtUtc <= now)
                throw new NotificationConflictException();

            var job = await db.NotificationJobs.SingleOrDefaultAsync(x => x.Kind == NotificationJobKind.TripReminder &&
                x.ActorId == input.UserId && x.TripPlanId == command.TripPlanId && x.SessionId == command.SessionId, ct);
            if (job is null)
            {
                job = new NotificationJobRow
                {
                    JobId = Guid.NewGuid(), ActorId = input.UserId, Kind = NotificationJobKind.TripReminder,
                    TripPlanId = command.TripPlanId, SessionId = command.SessionId, TargetId = command.TripPlanId,
                    Template = NotificationTemplate.TripReminder, TemplateVersion = 1
                };
                db.NotificationJobs.Add(job);
            }
            if (job.State == NotificationJobState.Published)
                throw new NotificationConflictException();

            job.State = NotificationJobState.Scheduled;
            job.Revision = checked(job.Revision + 1);
            job.DueAtUtc = command.DueAtUtc;
            job.PlannedStartDate = command.PlannedStartDate;
            job.TripVersion = input.TripVersion;
            Record(db, job, command.OperationId, action, fingerprint, now);
            return Receipt(job);
        }, ct);

    public Task<bool> CancelAsync(NotificationJobKind kind, string ownerId, Guid jobId, int revision,
        Guid operationId, DateTimeOffset now, CancellationToken ct) =>
        WriteAsync(async db =>
        {
            const string action = "CancelJob";
            var fingerprint = Fingerprint(new { kind, jobId, revision });
            if (await FindOperationAsync(db, ownerId, operationId, action, fingerprint, ct) is not null)
                return true;

            var job = await db.NotificationJobs.SingleOrDefaultAsync(x => x.JobId == jobId &&
                x.Kind == kind && x.ActorId == ownerId, ct);
            if (job is null)
                return false;
            if (job.Revision != revision || job.State != NotificationJobState.Scheduled)
                throw new NotificationConflictException();

            job.State = NotificationJobState.Cancelled;
            job.Revision = checked(job.Revision + 1);
            Record(db, job, operationId, action, fingerprint, now);
            return true;
        }, ct);

    public Task<bool> PublishIfCurrentAsync(Guid jobId, int revision, NotificationReminder reminder,
        DateTimeOffset now, CancellationToken ct) => WriteAsync(async db =>
    {
        var command = reminder.Command;
        var job = await db.NotificationJobs.SingleOrDefaultAsync(x => x.JobId == jobId && x.ActorId == reminder.UserId &&
            x.Kind == NotificationJobKind.TripReminder && x.State == NotificationJobState.Scheduled &&
            x.Revision == revision && x.TripPlanId == command.TripPlanId && x.SessionId == command.SessionId &&
            x.PlannedStartDate == command.PlannedStartDate && x.TripVersion == reminder.TripVersion &&
            x.DueAtUtc == command.DueAtUtc && x.DueAtUtc <= now, ct);
        if (job is null || !await IsCurrentTripAsync(db, reminder, ct))
            return false;

        var eventId = job.JobId; // One stable logical publication per job.
        db.NotificationEvents.Add(new NotificationEventRow
        {
            EventId = eventId, JobId = jobId, Category = NotificationCategory.Trip,
            Template = NotificationTemplate.TripReminder, TemplateVersion = job.TemplateVersion,
            TargetId = command.TripPlanId, CreatedAtUtc = now
        });
        db.NotificationRecipients.Add(new NotificationRecipientRow { EventId = eventId, UserId = job.ActorId });
        job.State = NotificationJobState.Published;
        return true;
    }, ct);

    public Task<bool> PublishAdminIfCurrentAsync(Guid jobId, int revision, INotificationAudienceDirectory directory,
        CancellationToken ct) => WriteAsync(async db =>
    {
        var now = clock.GetUtcNow();
        var job = await db.NotificationJobs.SingleOrDefaultAsync(x => x.JobId == jobId &&
            x.Kind == NotificationJobKind.AdminMessage && x.State == NotificationJobState.Scheduled &&
            x.Revision == revision && x.DueAtUtc <= now, ct);
        if (job is null || !await directory.CanManageAsync(job.ActorId, ct))
            return false;

        var ids = await db.NotificationJobRecipients.Where(x => x.JobId == jobId)
            .Select(x => x.UserId).ToListAsync(ct);
        if (ids.Count is < 1 or > 100)
            return false;
        var template = NotificationTemplates.Get(job.Template);
        if (template.Version != job.TemplateVersion)
            return false;
        var target = job.TargetId is { } id ? new NotificationTarget(template.Category, id) : null;
        var current = await directory.ResolveAsync(job.ActorId,
            new NotificationAudienceFilter(job.AudienceRole, job.AudienceDistrict, ids.ToArray()), template.Category, target, ct);
        var expected = ids.ToHashSet(StringComparer.Ordinal);
        if (!expected.SetEquals(current.Select(x => x.UserId)) ||
            current.Any(x => (job.AudienceRole is not null && x.Role != job.AudienceRole) ||
                (job.AudienceDistrict is not null &&
                    !string.Equals(job.AudienceDistrict, x.District, StringComparison.OrdinalIgnoreCase))))
            return false;

        var active = await db.Users.CountAsync(x => ids.Contains(x.UserId) && x.Status == UserAccountStatus.Active &&
            x.VerifiedAtUtc != null && x.DeletionRequestedAtUtc == null, ct);
        if (active != ids.Count)
            return false;

        db.NotificationEvents.Add(new NotificationEventRow
        {
            EventId = job.JobId, JobId = jobId, Category = template.Category, Template = job.Template,
            TemplateVersion = template.Version, TargetId = job.TargetId, CreatedAtUtc = now
        });
        db.NotificationRecipients.AddRange(ids.Select(userId =>
            new NotificationRecipientRow { EventId = job.JobId, UserId = userId }));
        job.State = NotificationJobState.Published;
        return true;
    }, ct);

    public async Task<NotificationDeliverySummary?> ReadSummaryAsync(string actorId, Guid jobId, CancellationToken ct)
    {
        await using var db = await OpenAsync(ct);
        if (!await db.NotificationJobs.AnyAsync(x => x.JobId == jobId && x.ActorId == actorId &&
            x.Kind == NotificationJobKind.AdminMessage, ct))
            return null;

        var counts = await (from row in db.NotificationRecipients
                            join evt in db.NotificationEvents on row.EventId equals evt.EventId
                            where evt.JobId == jobId
                            group row by 1 into g
                            select new
                            {
                                Queued = g.Count(x => x.State == NotificationDeliveryState.Pending),
                                Delivered = g.Count(x => x.State == NotificationDeliveryState.Delivered),
                                Suppressed = g.Count(x => x.State == NotificationDeliveryState.Suppressed),
                                Failed = g.Count(x => x.State == NotificationDeliveryState.DeadLettered),
                                Read = g.Count(x => x.ReadAtUtc != null)
                            }).SingleOrDefaultAsync(ct);
        if (counts is not null)
            return new(jobId, counts.Queued, counts.Delivered, counts.Suppressed, counts.Failed, counts.Read);

        var scheduledCount = await db.NotificationJobs.AnyAsync(x => x.JobId == jobId && x.ActorId == actorId &&
                x.State == NotificationJobState.Scheduled, ct)
            ? await db.NotificationJobRecipients.CountAsync(x => x.JobId == jobId, ct) : 0;
        return new(jobId, scheduledCount, 0, 0, 0, 0);
    }

    private static async Task<bool> IsCurrentTripAsync(WayhunterSharedDbContext db, NotificationReminder input,
        CancellationToken ct)
    {
        var command = input.Command;
        var trip = await db.Trips.AsNoTracking().Where(x => x.Id == command.TripPlanId)
            .Select(x => new { x.OwnerUserId, x.DraftStatus, x.StartDate, x.RowVersion }).SingleOrDefaultAsync(ct);
        return trip is not null && trip.OwnerUserId == input.UserId && trip.DraftStatus != TripDraftStatus.Cancelled &&
            trip.StartDate == command.PlannedStartDate && Convert.ToHexString(trip.RowVersion) == input.TripVersion &&
            await db.LiveTripSessions.AnyAsync(x => x.SessionId == command.SessionId && x.TripPlanId == command.TripPlanId &&
                (x.Status == LiveTripStatus.Active || x.Status == LiveTripStatus.Paused), ct) &&
            await db.Users.AnyAsync(x => x.UserId == input.UserId && x.Status == UserAccountStatus.Active &&
                x.VerifiedAtUtc != null && x.DeletionRequestedAtUtc == null, ct);
    }

    private static async Task<NotificationOperationRow?> FindOperationAsync(WayhunterSharedDbContext db, string actor,
        Guid operationId, string action, string fingerprint, CancellationToken ct)
    {
        var record = await db.NotificationOperations.FindAsync([actor, operationId], ct);
        if (record is not null && (record.Action != action || record.Fingerprint != fingerprint))
            throw new NotificationConflictException();
        return record;
    }

    private static void Record(WayhunterSharedDbContext db, NotificationJobRow job, Guid operation, string action,
        string fingerprint, DateTimeOffset now) => db.NotificationOperations.Add(new NotificationOperationRow
        {
            ActorId = job.ActorId, OperationId = operation, ResourceId = job.JobId, Action = action,
            Fingerprint = fingerprint, JobState = job.State, Revision = job.Revision, DueAtUtc = job.DueAtUtc,
            RecordedAtUtc = now
        });

    private static string Fingerprint<T>(T value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
    private static NotificationJobReceipt Receipt(NotificationJobRow job) => new(job.JobId, job.Revision, job.State, job.DueAtUtc);
    private static NotificationJobReceipt Receipt(NotificationOperationRow row) =>
        new(row.ResourceId, row.Revision!.Value, row.JobState!.Value, row.DueAtUtc!.Value);

    private async Task<WayhunterSharedDbContext> OpenAsync(CancellationToken ct)
    {
        var db = await contexts.CreateDbContextAsync(ct);
        if (db.Database.IsSqlServer())
            return db;
        await db.DisposeAsync();
        throw new NotificationUnavailableException();
    }

    private async Task<T> WriteAsync<T>(Func<WayhunterSharedDbContext, Task<T>> action, CancellationToken ct)
    {
        await using var strategy = await OpenAsync(ct);
        return await strategy.Database.CreateExecutionStrategy().ExecuteAsync(async token =>
        {
            await using var db = await OpenAsync(token);
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
            var result = await action(db);
            await db.SaveChangesAsync(token);
            await tx.CommitAsync(token);
            return result;
        }, ct);
    }
}
