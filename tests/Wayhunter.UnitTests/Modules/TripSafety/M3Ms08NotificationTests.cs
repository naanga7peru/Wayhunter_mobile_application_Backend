using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Wayhunter.Application.Modules.TripSafety.Interfaces;
using Wayhunter.Application.Modules.TripSafety.Notifications;
using Wayhunter.Domain.Modules.TravelPlanning.Entities;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Wayhunter.Infrastructure.Persistence.Models.TripSafety;
using Wayhunter.Infrastructure.Persistence.Repositories.TravelPlanning;
using Wayhunter.Infrastructure.Persistence.Repositories.TripSafety;
using Xunit;

namespace Wayhunter.UnitTests.Modules.TripSafety;

public sealed class M3Ms08NotificationTests
{
    [Fact]
    public void SharedModelHasPrivateRecipientKeyAndProtectedTokenMetadata()
    {
        using var db = new WayhunterSharedDbContext(new DbContextOptionsBuilder<WayhunterSharedDbContext>()
            .UseSqlServer("Server=localhost;Database=ModelOnly;Integrated Security=True").Options);
        var model = db.GetService<IDesignTimeModel>().Model;
        var recipient = model.FindEntityType(typeof(NotificationRecipientRow))!;
        var device = model.FindEntityType(typeof(NotificationDeviceRow))!;
        var job = model.FindEntityType(typeof(NotificationJobRow))!;
        Assert.Equal(new[] { "EventId", "UserId" }, recipient.FindPrimaryKey()!.Properties.Select(x => x.Name));
        Assert.Contains(recipient.GetIndexes(), x => x.GetFilter() == "[State] = 0");
        Assert.Contains(recipient.GetCheckConstraints(), x => x.Name == "CK_MS08_NotificationRecipient_Lease");
        Assert.Contains(device.GetIndexes(), x => x.IsUnique && x.Properties.Single().Name == "TokenFingerprint");
        Assert.Equal(typeof(byte[]), device.FindProperty("ProtectedToken")!.ClrType);
        Assert.DoesNotContain(device.GetProperties(), x => x.Name == "Token");
        Assert.True(device.FindProperty("RowVersion")!.IsConcurrencyToken);
        Assert.Equal("Fingerprint", Assert.Single(model.FindEntityType(typeof(NotificationTokenBindingRow))!
            .FindPrimaryKey()!.Properties).Name);
        Assert.Contains(job.GetForeignKeys(), x => x.Properties.Select(p => p.Name).SequenceEqual(["SessionId", "TripPlanId"]));
        Assert.Contains(job.GetIndexes(), x => x.IsUnique && x.GetFilter() == "[Kind] = 1");
        var snapshot = db.GetService<IMigrationsAssembly>().ModelSnapshot!.Model;
        Assert.Contains(snapshot.GetEntityTypes(), x => x.GetTableName() == "TripSafety_NotificationJobs");
        Assert.Contains(snapshot.GetEntityTypes(), x => x.GetTableName() == "TripSafety_NotificationDevices");
    }

    [Theory]
    [InlineData("password=123")]
    [InlineData("51.000, 1.000")]
    [InlineData("travel promotion")]
    public async Task ComposerRejectsAllUnapprovedTextAndLinks(string text)
    {
        var f = new ComposerFixture();
        await Assert.ThrowsAsync<ArgumentException>(() => f.Service.PreviewAsync(f.Actor, f.Command with { Text = text }, default));
        await Assert.ThrowsAsync<ArgumentException>(() => f.Service.PreviewAsync(f.Actor,
            f.Command with { DeepLink = "https://external.invalid/private" }, default));
        await Assert.ThrowsAsync<ArgumentException>(() => f.Service.PreviewAsync(f.Actor,
            f.Command with { Target = new NotificationTarget(NotificationCategory.Safety, Guid.NewGuid()) }, default));
        Assert.Equal(0, f.DirectoryCalls);
    }

    [Fact]
    public async Task ComposerEnforcesAdminAndEveryAudienceFilter()
    {
        var f = new ComposerFixture { Authorized = false };
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Service.PreviewAsync(f.Actor, f.Command, default));
        Assert.Equal(0, f.DirectoryCalls);
        f.Authorized = true;
        var preview = await f.Service.PreviewAsync(f.Actor, f.Command, default);
        Assert.Equal(2, preview.Count);
        var originals = f.Members;
        f.Members = [originals[0] with { District = "Other" }];
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Service.PreviewAsync(f.Actor, f.Command, default));
        f.Members = [originals[0] with { Role = NotificationAudienceRole.Administrator }];
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Service.PreviewAsync(f.Actor, f.Command, default));
        f.Members = [originals[0] with { UserId = NewUser() }];
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Service.PreviewAsync(f.Actor, f.Command, default));
    }

    [Fact]
    public async Task BulkConfirmationIsScopedAndDuplicateSchedulePreservesOneJob()
    {
        var f = new ComposerFixture();
        var preview = await f.Service.PreviewAsync(f.Actor, f.Command, default);
        await Assert.ThrowsAsync<NotificationConflictException>(() =>
            f.Service.ScheduleAsync(f.Actor, f.Command, false, preview.ReceiptId, default));
        await Assert.ThrowsAsync<NotificationConflictException>(() =>
            f.Service.ScheduleAsync(f.Actor, f.Command with { Template = NotificationTemplate.VoucherUpdate },
                true, preview.ReceiptId, default));

        var scheduled = await f.Service.ScheduleAsync(f.Actor, f.Command, true, preview.ReceiptId, default);
        var retry = await f.Service.ScheduleAsync(f.Actor, f.Command, true, preview.ReceiptId, default);
        Assert.Equal(scheduled, retry);
        Assert.Equal(1, f.Jobs.Writes);
        Assert.True(await f.Service.CancelAsync(f.Actor, scheduled.JobId, scheduled.Revision, Guid.NewGuid(), default));
        Assert.Equal(NotificationJobState.Cancelled, f.Jobs.Job!.State);
        Assert.Equal(0, (await f.Service.SummaryAsync(f.Actor, scheduled.JobId, default))!.Queued);
    }

    [Fact]
    public async Task ReminderRevisionCancellationAndTripChangeSuppressStaleWork()
    {
        var clock = new TestClock();
        var owner = NewUser();
        var day = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var trip = new TripPlan(owner, "Trip", day, day.AddDays(1), 100m, "Car", 2);
        var versionProperty = typeof(TripPlan).GetProperty(nameof(TripPlan.RowVersion))!;
        versionProperty.SetValue(trip, new byte[8]);
        var trips = new InMemoryTripPlanRepository();
        await trips.AddAsync(trip);
        var guard = new Guard();
        var jobs = new Jobs();
        var service = new NotificationReminderService(trips, guard, jobs, clock);
        var execution = new NotificationReminderExecutionService(trips, guard, jobs, clock);
        var request = new NotificationReminderCommand(Guid.NewGuid(), trip.Id, Guid.NewGuid(),
            day, clock.GetUtcNow().AddMinutes(1));

        var old = await service.ScheduleAsync(owner, request, default);
        var oldWork = jobs.Reminder!;
        var later = await service.ScheduleAsync(owner, request with
        {
            OperationId = Guid.NewGuid(), DueAtUtc = request.DueAtUtc.AddMinutes(1)
        }, default);
        Assert.Equal(old.JobId, later.JobId);
        Assert.Equal(old.Revision + 1, later.Revision);

        clock.Advance(TimeSpan.FromMinutes(3));
        Assert.False(await execution.TryPublishAsync(old.JobId, old.Revision, oldWork, default));
        guard.Allowed = false;
        Assert.False(await execution.TryPublishAsync(later.JobId, later.Revision, jobs.Reminder!, default));
        guard.Allowed = true;
        versionProperty.SetValue(trip, new byte[] { 1, 0, 0, 0, 0, 0, 0, 0 });
        Assert.False(await execution.TryPublishAsync(later.JobId, later.Revision, jobs.Reminder!, default));
        versionProperty.SetValue(trip, new byte[8]);
        Assert.True(await service.CancelAsync(owner, later.JobId, later.Revision, Guid.NewGuid(), default));
        Assert.False(await execution.TryPublishAsync(later.JobId, later.Revision, jobs.Reminder!, default));
        trip.Cancel();
        Assert.False(await execution.TryPublishAsync(later.JobId, later.Revision, jobs.Reminder!, default));
        Assert.Equal(0, jobs.Publications);
        Assert.Equal(request.SessionId, guard.LastSession);
    }

    [Fact]
    public async Task PushFailureLeavesInboxAndStableEventSurvivesRetry()
    {
        var f = new DispatchFixture { Throw = true };
        Assert.Equal(NotificationDispatchOutcome.Retry, await f.Dispatch.ProcessNextAsync(default));
        Assert.Equal("RetryScheduled", Assert.Single(f.Store.Audit).Outcome);
        Assert.Equal(NotificationDispatchOutcome.NoWork, await f.Dispatch.ProcessNextAsync(default));
        f.Clock.Advance(TimeSpan.FromSeconds(30));
        f.Throw = false;
        Assert.Equal(NotificationDispatchOutcome.Delivered, await f.Dispatch.ProcessNextAsync(default));
        Assert.Equal(new[] { f.Store.EventId, f.Store.EventId }, f.Sent);
        Assert.True(f.Store.InboxExists);
        Assert.Equal(2, f.Store.Audit.Count);
    }

    [Theory]
    [InlineData(false, NotificationPushResult.Delivered, "PushDenied", 0)]
    [InlineData(true, NotificationPushResult.InvalidDevice, "InvalidDevice", 1)]
    [InlineData(true, NotificationPushResult.NoActiveDevice, "NoActiveDevice", 1)]
    public async Task NoAuthorizedDeviceSuppressesPushButLeavesInbox(bool allowed, NotificationPushResult result,
        string reason, int calls)
    {
        var f = new DispatchFixture { Allowed = allowed, Result = result };
        Assert.Equal(NotificationDispatchOutcome.Suppressed, await f.Dispatch.ProcessNextAsync(default));
        Assert.Equal(reason, Assert.Single(f.Store.Audit).Outcome);
        Assert.Equal(calls, f.Sent.Count);
        Assert.True(f.Store.InboxExists);
    }

    [Fact]
    public async Task ConcurrentWorkersDoNotSendWhileLeaseIsLiveAndRejectStaleAck()
    {
        var f = new DispatchFixture();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var worker = f.Dispatch.ProcessNextAsync(default);
        Assert.Equal(NotificationDispatchOutcome.Delivered, await worker);

        f = new DispatchFixture();
        f.BeforeReturn = async () => { entered.TrySetResult(); await release.Task; };
        var old = f.Dispatch.ProcessNextAsync(default);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(NotificationDispatchOutcome.NoWork, await f.Dispatch.ProcessNextAsync(default));
            f.Clock.Advance(TimeSpan.FromMinutes(3));
            f.BeforeReturn = null;
            Assert.Equal(NotificationDispatchOutcome.Delivered, await f.Dispatch.ProcessNextAsync(default));
        }
        finally
        {
            release.TrySetResult();
        }
        Assert.Equal(NotificationDispatchOutcome.LeaseLost, await old);
        Assert.Equal(new[] { f.Store.EventId, f.Store.EventId }, f.Sent);
        Assert.Single(f.Store.Audit);
    }

    [Fact]
    public async Task ProviderTimeoutEventuallyDeadLettersWithoutDeletingInbox()
    {
        var f = new DispatchFixture { Throw = true };
        Assert.Equal(NotificationDispatchOutcome.Retry, await f.Dispatch.ProcessNextAsync(default));
        f.Clock.Advance(TimeSpan.FromSeconds(30));
        Assert.Equal(NotificationDispatchOutcome.DeadLettered, await f.Dispatch.ProcessNextAsync(default));
        Assert.Equal("AttemptLimit", f.Store.Audit.Last().Outcome);
        Assert.True(f.Store.InboxExists);
    }

    [Theory]
    [InlineData("bad")]
    [InlineData("contains whitespace and another part")]
    [InlineData("contains/a/forbidden/token")]
    public void MalformedDeviceTokenIsRejected(string token) =>
        Assert.Throws<ArgumentException>(() => NotificationInputs.ValidateDevice(Guid.NewGuid(), Guid.NewGuid(), token));

    private static string NewUser() => Guid.NewGuid().ToString("D");

    private sealed class TestClock : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 11, 20, 10, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan elapsed) => _now += elapsed;
    }

    private sealed class ComposerFixture : INotificationAudienceDirectory
    {
        public string Actor { get; } = NewUser();
        public bool Authorized { get; set; } = true;
        public int DirectoryCalls { get; private set; }
        public NotificationAudienceMember[] Members { get; set; }
        public NotificationAdminCommand Command { get; }
        public Jobs Jobs { get; } = new();
        public NotificationComposerService Service { get; }

        public ComposerFixture()
        {
            Members = [new(NewUser(), NotificationAudienceRole.Member, "Colombo"), new(NewUser(), NotificationAudienceRole.Member, "Colombo")];
            Command = new(Guid.NewGuid(), NotificationTemplate.CoinsUpdate, null,
                new(NotificationAudienceRole.Member, "Colombo", Members.Select(x => x.UserId).ToArray()),
                null, null, null);
            Service = new(this, Jobs, new TestClock());
        }

        public Task<bool> CanManageAsync(string actorId, CancellationToken ct) => Task.FromResult(Authorized && actorId == Actor);
        public Task<IReadOnlyList<NotificationAudienceMember>> ResolveAsync(string actorId, NotificationAudienceFilter filter,
            NotificationCategory category, NotificationTarget? target, CancellationToken ct)
        {
            DirectoryCalls++;
            return Task.FromResult<IReadOnlyList<NotificationAudienceMember>>(Members);
        }
    }

    private sealed class Guard : ITripFinishedReminderGuard
    {
        public bool Allowed { get; set; } = true;
        public Guid LastSession { get; private set; }
        public Task<bool> CanDispatchAsync(Guid tripPlanId, Guid sessionId, CancellationToken cancellationToken = default)
        {
            LastSession = sessionId;
            return Task.FromResult(Allowed);
        }
    }

    private sealed class Jobs : INotificationJobs, INotificationReminderPublisher
    {
        private NotificationAudienceReceipt? _preview;
        private NotificationAdminSchedule? _admin;
        public NotificationJobReceipt? Job { get; private set; }
        public NotificationReminder? Reminder { get; private set; }
        public int Writes { get; private set; }
        public int Publications { get; private set; }

        public Task SavePreviewAsync(NotificationAudienceReceipt receipt, CancellationToken ct)
        {
            _preview = receipt;
            return Task.CompletedTask;
        }

        public Task<NotificationJobReceipt> ScheduleAdminAsync(NotificationAdminSchedule command, CancellationToken ct)
        {
            if (_admin is not null && command.Fingerprint == _admin.Fingerprint)
                return Task.FromResult(Job!);
            if (_preview is null || command.PreviewReceiptId != _preview.ReceiptId ||
                command.ActorId != _preview.ActorId || command.Fingerprint != _preview.Fingerprint ||
                command.Now >= _preview.ExpiresAtUtc)
                throw new NotificationConflictException();

            _admin = command;
            _preview = null;
            Writes++;
            Job = new(Guid.NewGuid(), 1, NotificationJobState.Scheduled, command.Command.SendAtUtc ?? command.Now);
            return Task.FromResult(Job);
        }

        public Task<NotificationJobReceipt> ScheduleReminderAsync(NotificationReminder reminder, CancellationToken ct)
        {
            Reminder = reminder;
            Job = new(Job?.JobId ?? Guid.NewGuid(), (Job?.Revision ?? 0) + 1,
                NotificationJobState.Scheduled, reminder.Command.DueAtUtc);
            return Task.FromResult(Job);
        }

        public Task<bool> CancelAsync(NotificationJobKind kind, string ownerId, Guid jobId, int revision,
            Guid operationId, DateTimeOffset now, CancellationToken ct)
        {
            if (Job is null || Job.JobId != jobId || Job.Revision != revision || Job.State != NotificationJobState.Scheduled)
                return Task.FromResult(false);
            Job = Job with { State = NotificationJobState.Cancelled, Revision = revision + 1 };
            return Task.FromResult(true);
        }

        public Task<NotificationDeliverySummary?> ReadSummaryAsync(string actorId, Guid jobId, CancellationToken ct) =>
            Task.FromResult<NotificationDeliverySummary?>(new(jobId, Job?.State == NotificationJobState.Scheduled ? 2 : 0, 0, 0, 0, 0));

        public Task<bool> PublishIfCurrentAsync(Guid jobId, int revision, NotificationReminder reminder, DateTimeOffset now,
            CancellationToken ct)
        {
            if (Job?.JobId != jobId || Job.Revision != revision || Job.State != NotificationJobState.Scheduled)
                return Task.FromResult(false);
            Job = Job with { State = NotificationJobState.Published };
            Publications++;
            return Task.FromResult(true);
        }
    }

    private sealed class DispatchFixture : INotificationPushEligibility, INotificationPushProvider
    {
        public TestClock Clock { get; } = new();
        public DeliveryStore Store { get; } = new();
        public NotificationDispatchService Dispatch { get; }
        public List<Guid> Sent { get; } = [];
        public bool Allowed { get; set; } = true;
        public bool Throw { get; set; }
        public NotificationPushResult Result { get; set; }
        public Func<Task>? BeforeReturn { get; set; }

        public DispatchFixture() => Dispatch = new(Store, this, this, Clock, new M3OutboxWorkerSettings { MaxAttempts = 2 });
        public Task<bool> CanPushAsync(NotificationLease lease, CancellationToken ct) => Task.FromResult(Allowed);
        public async Task<NotificationPushResult> SendAsync(Guid eventId, string recipientId, NotificationContent content, CancellationToken ct)
        {
            Sent.Add(eventId);
            Assert.Equal("Open Wayhunter to view the update.", content.PushBody);
            if (BeforeReturn is { } barrier)
                await barrier();
            if (Throw)
                throw new TimeoutException("provider details must not leak");
            return Result;
        }
    }

    private sealed class DeliveryStore : INotificationDeliveryStore
    {
        private readonly object _sync = new();
        private NotificationLease? _lease;
        private NotificationDeliveryState _state;
        private DateTimeOffset? _next;
        private int _attempt;
        public Guid EventId { get; } = Guid.NewGuid();
        public string UserId { get; } = NewUser();
        public bool InboxExists => true;
        public List<NotificationAttemptResult> Audit { get; } = [];

        public Task<NotificationLease?> ClaimAsync(DateTimeOffset now, int maxAttempts, TimeSpan leaseDuration, CancellationToken ct)
        {
            lock (_sync)
            {
                if (_state != NotificationDeliveryState.Pending || _next > now || _lease?.ExpiresAtUtc > now ||
                    _attempt >= maxAttempts)
                    return Task.FromResult<NotificationLease?>(null);
                _lease = new(Guid.NewGuid(), EventId, UserId, ++_attempt, now.Add(leaseDuration),
                    NotificationCategory.Coins, NotificationTemplate.CoinsUpdate, 1);
                return Task.FromResult<NotificationLease?>(_lease);
            }
        }
        public Task<bool> FinishAsync(NotificationLease lease, NotificationAttemptResult result, DateTimeOffset now, CancellationToken ct)
        {
            lock (_sync)
            {
                if (_lease != lease || lease.ExpiresAtUtc <= now)
                    return Task.FromResult(false);
                _lease = null;
                _state = result.State;
                _next = result.RetryAtUtc;
                Audit.Add(result);
                return Task.FromResult(true);
            }
        }
    }
}
