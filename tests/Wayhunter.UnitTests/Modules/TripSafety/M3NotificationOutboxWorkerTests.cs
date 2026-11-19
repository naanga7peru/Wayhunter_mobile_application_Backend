
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Wayhunter.Api.Extensions;

using Wayhunter.Infrastructure.Integrations.Notifications;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Wayhunter.Infrastructure.Persistence.Models.TripSafety;
using Wayhunter.Infrastructure.Persistence.Repositories.TripSafety;

using Xunit;

namespace Wayhunter.UnitTests.Modules.TripSafety;

public sealed class M3NotificationOutboxWorkerTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 11, 19, 8, 0, 0, TimeSpan.Zero);

    private sealed class MutableClock : TimeProvider
    {
        public DateTimeOffset Current { get; set; } = Now;

        public override DateTimeOffset GetUtcNow()
        {
            return Current;
        }
    }

    private sealed class FakeSender
        : ITripMembershipRevocationNotificationSender
    {
        public int FailRemaining { get; set; }

        public int Calls { get; private set; }

        public List<Guid> SentIds { get; } = new();

        public Task SendAsync(
            TripMembershipRevocationNotification notification,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Calls++;
            SentIds.Add(notification.NotificationId);

            if (FailRemaining > 0)
            {
                FailRemaining--;

                throw new InvalidOperationException(
                    "Private simulated transport failure.");
            }

            return Task.CompletedTask;
        }
    }

    private sealed class FakeHttpHandler : HttpMessageHandler
    {
        public HttpStatusCode ResponseStatus { get; set; } =
            HttpStatusCode.Accepted;

        public string? IdempotencyKey { get; private set; }

        public bool HasSecretHeader { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            IdempotencyKey = request.Headers
                .GetValues("Idempotency-Key")
                .Single();

            HasSecretHeader = request.Headers.Contains(
                "X-Wayhunter-Webhook-Secret");

            return Task.FromResult(
                new HttpResponseMessage(ResponseStatus));
        }
    }

    private static WayhunterSharedDbContext CreateDb()
    {
        var options =
            new DbContextOptionsBuilder<WayhunterSharedDbContext>()
                .UseInMemoryDatabase(
                    $"m3-notification-worker-{Guid.NewGuid():N}")
                .Options;

        return new WayhunterSharedDbContext(options);
    }

    private static TripMembershipRevocationOutboxRow Pending()
    {
        return new TripMembershipRevocationOutboxRow
        {
            Id = Guid.NewGuid(),
            TripPlanId = Guid.NewGuid(),
            MemberId = Guid.NewGuid(),
            MemberUserId = Guid.NewGuid().ToString("D"),
            RemovedAtUtc = Now.AddMinutes(-10),
            CreatedAtUtc = Now.AddMinutes(-9),
            IsDelivered = false,
            DeliveredAtUtc = null,
            DeliveryAttempts = 0,
            NextAttemptAtUtc = null,
            LeaseToken = null,
            LeaseUntilUtc = null,
            DeadLetteredAtUtc = null,
            LastError = null
        };
    }

    private static M3OutboxWorkerSettings Settings(
        int maxAttempts = 3)
    {
        return new M3OutboxWorkerSettings
        {
            MaxAttempts = maxAttempts,
            BaseRetrySeconds = 10,
            MaxRetrySeconds = 60,
            LeaseSeconds = 120,
            PollSeconds = 5,
            BatchSize = 10
        };
    }

    private static M3NotificationOutboxDispatcher Dispatcher(
        WayhunterSharedDbContext db,
        FakeSender sender,
        MutableClock clock,
        M3OutboxWorkerSettings? settings = null)
    {
        return new M3NotificationOutboxDispatcher(
            db,
            sender,
            clock,
            settings ?? Settings());
    }

    [Fact]
    public async Task EmptyOutbox_ReturnsNoWork()
    {
        await using var db = CreateDb();

        var sender = new FakeSender();
        var clock = new MutableClock();

        var dispatcher = Dispatcher(db, sender, clock);

        var outcome = await dispatcher.ProcessNextAsync();

        Assert.Equal(
            M3OutboxDispatchOutcome.NoWork,
            outcome);

        Assert.Equal(0, sender.Calls);
    }

    [Fact]
    public async Task SuccessfulDelivery_MarksDeliveredOnlyOnce()
    {
        await using var db = CreateDb();

        var pending = Pending();

        db.TripMembershipRevocationOutbox.Add(pending);
        await db.SaveChangesAsync();

        var sender = new FakeSender();
        var clock = new MutableClock();

        var dispatcher = Dispatcher(db, sender, clock);

        var first = await dispatcher.ProcessNextAsync();

        Assert.Equal(
            M3OutboxDispatchOutcome.Delivered,
            first);

        Assert.True(pending.IsDelivered);
        Assert.Equal(Now, pending.DeliveredAtUtc);
        Assert.Equal(1, pending.DeliveryAttempts);

        Assert.Null(pending.LeaseToken);
        Assert.Null(pending.LeaseUntilUtc);
        Assert.Null(pending.NextAttemptAtUtc);

        var second = await dispatcher.ProcessNextAsync();

        Assert.Equal(
            M3OutboxDispatchOutcome.NoWork,
            second);

        Assert.Equal(1, sender.Calls);
    }

    [Fact]
    public async Task FailedDelivery_SchedulesBackoff()
    {
        await using var db = CreateDb();

        var pending = Pending();

        db.TripMembershipRevocationOutbox.Add(pending);
        await db.SaveChangesAsync();

        var sender = new FakeSender
        {
            FailRemaining = 1
        };

        var clock = new MutableClock();

        var dispatcher = Dispatcher(db, sender, clock);

        var outcome = await dispatcher.ProcessNextAsync();

        Assert.Equal(
            M3OutboxDispatchOutcome.RetryScheduled,
            outcome);

        Assert.False(pending.IsDelivered);
        Assert.Null(pending.DeliveredAtUtc);

        Assert.Equal(1, pending.DeliveryAttempts);

        Assert.Equal(
            Now.AddSeconds(10),
            pending.NextAttemptAtUtc);

        Assert.Null(pending.LeaseToken);
        Assert.Null(pending.LeaseUntilUtc);

        Assert.Equal(
            nameof(InvalidOperationException),
            pending.LastError);

        // Never expose exception.Message in the row.
        Assert.DoesNotContain(
            "Private simulated",
            pending.LastError!);
    }

    [Fact]
    public async Task Retry_UsesSameNotificationId()
    {
        await using var db = CreateDb();

        var pending = Pending();

        db.TripMembershipRevocationOutbox.Add(pending);
        await db.SaveChangesAsync();

        var sender = new FakeSender
        {
            FailRemaining = 1
        };

        var clock = new MutableClock();

        var dispatcher = Dispatcher(db, sender, clock);

        Assert.Equal(
            M3OutboxDispatchOutcome.RetryScheduled,
            await dispatcher.ProcessNextAsync());

        // Retry cannot occur before due time.
        clock.Current = Now.AddSeconds(9);

        Assert.Equal(
            M3OutboxDispatchOutcome.NoWork,
            await dispatcher.ProcessNextAsync());

        clock.Current = Now.AddSeconds(10);

        Assert.Equal(
            M3OutboxDispatchOutcome.Delivered,
            await dispatcher.ProcessNextAsync());

        Assert.True(pending.IsDelivered);
        Assert.Equal(2, pending.DeliveryAttempts);
        Assert.Equal(2, sender.Calls);

        Assert.All(
            sender.SentIds,
            id => Assert.Equal(pending.Id, id));

        Assert.Null(pending.LastError);
    }

    [Fact]
    public async Task MaxAttempts_CreatesDeadLetter()
    {
        await using var db = CreateDb();

        var pending = Pending();

        db.TripMembershipRevocationOutbox.Add(pending);
        await db.SaveChangesAsync();

        var sender = new FakeSender
        {
            FailRemaining = 10
        };

        var clock = new MutableClock();

        var dispatcher = Dispatcher(
            db,
            sender,
            clock,
            Settings(maxAttempts: 2));

        Assert.Equal(
            M3OutboxDispatchOutcome.RetryScheduled,
            await dispatcher.ProcessNextAsync());

        clock.Current = Now.AddSeconds(10);

        Assert.Equal(
            M3OutboxDispatchOutcome.DeadLettered,
            await dispatcher.ProcessNextAsync());

        Assert.False(pending.IsDelivered);

        Assert.Equal(2, pending.DeliveryAttempts);
        Assert.Equal(clock.Current, pending.DeadLetteredAtUtc);

        Assert.Null(pending.NextAttemptAtUtc);
        Assert.Null(pending.LeaseToken);

        clock.Current = Now.AddHours(3);

        Assert.Equal(
            M3OutboxDispatchOutcome.NoWork,
            await dispatcher.ProcessNextAsync());

        Assert.Equal(2, sender.Calls);
    }

    [Fact]
    public async Task ActiveLease_PreventsSecondClaim()
    {
        await using var db = CreateDb();

        var pending = Pending();

        pending.LeaseToken = Guid.NewGuid();
        pending.LeaseUntilUtc = Now.AddMinutes(1);

        db.TripMembershipRevocationOutbox.Add(pending);
        await db.SaveChangesAsync();

        var sender = new FakeSender();
        var clock = new MutableClock();

        var dispatcher = Dispatcher(db, sender, clock);

        Assert.Equal(
            M3OutboxDispatchOutcome.NoWork,
            await dispatcher.ProcessNextAsync());

        Assert.Equal(0, sender.Calls);
        Assert.False(pending.IsDelivered);
    }

    [Fact]
    public async Task ExpiredLease_CanBeRecovered()
    {
        await using var db = CreateDb();

        var pending = Pending();

        pending.LeaseToken = Guid.NewGuid();
        pending.LeaseUntilUtc = Now.AddSeconds(-1);

        db.TripMembershipRevocationOutbox.Add(pending);
        await db.SaveChangesAsync();

        var sender = new FakeSender();
        var clock = new MutableClock();

        var dispatcher = Dispatcher(db, sender, clock);

        Assert.Equal(
            M3OutboxDispatchOutcome.Delivered,
            await dispatcher.ProcessNextAsync());

        Assert.True(pending.IsDelivered);
        Assert.Null(pending.LeaseToken);
        Assert.Equal(1, sender.Calls);
    }

    [Fact]
    public async Task Monitoring_ReturnsAggregateCounts()
    {
        await using var db = CreateDb();

        var pending = Pending();

        var delivered = Pending();
        delivered.IsDelivered = true;
        delivered.DeliveredAtUtc = Now;

        var dead = Pending();
        dead.DeliveryAttempts = 3;
        dead.DeadLetteredAtUtc = Now;

        db.TripMembershipRevocationOutbox.AddRange(
            pending,
            delivered,
            dead);

        await db.SaveChangesAsync();

        var dispatcher = Dispatcher(
            db,
            new FakeSender(),
            new MutableClock());

        var status = await dispatcher.GetStatusAsync();

        Assert.Equal(1, status.Ready);
        Assert.Equal(0, status.Delayed);
        Assert.Equal(0, status.Leased);
        Assert.Equal(1, status.DeadLettered);
        Assert.Equal(1, status.Delivered);
        Assert.Equal(0, status.Exhausted);
    }

    [Fact]
    public void Backoff_IsExponentialAndCapped()
    {
        Assert.Equal(
            TimeSpan.FromSeconds(30),
            M3OutboxRetryPolicy.CalculateDelay(1, 30, 300));

        Assert.Equal(
            TimeSpan.FromSeconds(60),
            M3OutboxRetryPolicy.CalculateDelay(2, 30, 300));

        Assert.Equal(
            TimeSpan.FromSeconds(120),
            M3OutboxRetryPolicy.CalculateDelay(3, 30, 300));

        Assert.Equal(
            TimeSpan.FromSeconds(300),
            M3OutboxRetryPolicy.CalculateDelay(10, 30, 300));
    }

    [Fact]
    public void InvalidWorkerSettings_AreRejected()
    {
        var settings = Settings();
        settings.MaxAttempts = 0;

        Assert.Throws<InvalidOperationException>(
            settings.Validate);
    }

    [Fact]
    public void Webhook_RequiresHttpsAndSecret()
    {
        var settings = new M3OutboxWebhookSettings
        {
            WebhookUrl = "http://example.test/notify",
            WebhookApiKey = new string('x', 32)
        };

        Assert.Throws<InvalidOperationException>(
            settings.Validate);

        settings.WebhookUrl =
            "https://example.test/notify";

        settings.Validate();
    }

    [Fact]
    public async Task Webhook_SendsStableIdempotencyHeader()
    {
        var handler = new FakeHttpHandler();

        using var httpClient = new HttpClient(handler);

        var settings = new M3OutboxWebhookSettings
        {
            WebhookUrl = "https://example.test/notify",
            WebhookApiKey = new string('x', 32)
        };

        var sender =
            new WebhookTripMembershipRevocationNotificationSender(
                httpClient,
                settings);

        var notification =
            new TripMembershipRevocationNotification(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid().ToString("D"),
                Now);

        await sender.SendAsync(notification);

        Assert.Equal(
            notification.NotificationId.ToString("D"),
            handler.IdempotencyKey);

        Assert.True(handler.HasSecretHeader);
    }

    [Fact]
    public async Task Webhook_NonSuccess_DoesNotAcknowledge()
    {
        var handler = new FakeHttpHandler
        {
            ResponseStatus = HttpStatusCode.ServiceUnavailable
        };

        using var httpClient = new HttpClient(handler);

        var sender =
            new WebhookTripMembershipRevocationNotificationSender(
                httpClient,
                new M3OutboxWebhookSettings
                {
                    WebhookUrl = "https://example.test/notify",
                    WebhookApiKey = new string('x', 32)
                });

        await Assert.ThrowsAsync<HttpRequestException>(
            () => sender.SendAsync(
                new TripMembershipRevocationNotification(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    Guid.NewGuid().ToString("D"),
                    Now)));
    }

    [Fact]
    public void DisabledWorker_DoesNotRegisterHostedService()
    {
        var services = new ServiceCollection();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["TripSafety:EnableNotificationOutboxWorker"] =
                        "false"
                })
            .Build();

        services.AddConfiguredTripSafetyNotificationOutboxWorker(
            configuration);

        Assert.DoesNotContain(
            services,
            x => x.ServiceType == typeof(IHostedService));
    }

    [Fact]
    public void WorkerEnabledWithoutSql_ShouldRejectConfiguration()
    {
        var services = new ServiceCollection();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["TripSafety:EnableNotificationOutboxWorker"] =
                        "true",

                    ["TripSafety:EnableSqlPersistence"] =
                        "false"
                })
            .Build();

        Assert.Throws<InvalidOperationException>(() =>
            services.AddConfiguredTripSafetyNotificationOutboxWorker(
                configuration));
    }

    [Fact]
    public void EfModel_ContainsNewRetryLeaseProperties()
    {
        using var db = CreateDb();

        var entity = db.Model.FindEntityType(
            typeof(TripMembershipRevocationOutboxRow));

        Assert.NotNull(entity);

        Assert.NotNull(entity.FindProperty(
            nameof(TripMembershipRevocationOutboxRow.NextAttemptAtUtc)));

        Assert.NotNull(entity.FindProperty(
            nameof(TripMembershipRevocationOutboxRow.LeaseToken)));

        Assert.NotNull(entity.FindProperty(
            nameof(TripMembershipRevocationOutboxRow.LeaseUntilUtc)));

        Assert.NotNull(entity.FindProperty(
            nameof(TripMembershipRevocationOutboxRow.DeadLetteredAtUtc)));

        Assert.NotNull(entity.FindProperty(
            nameof(TripMembershipRevocationOutboxRow.LastError)));
    }
}
