
using System;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;

using Wayhunter.Infrastructure.Persistence.DbContexts;
using Wayhunter.Infrastructure.Persistence.Models.TripSafety;
using Wayhunter.Infrastructure.Persistence.Repositories.TripSafety;

using Xunit;

namespace Wayhunter.UnitTests.Modules.TripSafety;

public sealed class M3RevocationOutboxProcessorTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 11, 18, 10, 0, 0, TimeSpan.Zero);

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class FakeSender
        : ITripMembershipRevocationNotificationSender
    {
        public bool ShouldFail { get; set; }

        public int Calls { get; private set; }

        public TripMembershipRevocationNotification?
            LastNotification
        { get; private set; }

        public Task SendAsync(
            TripMembershipRevocationNotification notification,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Calls++;
            LastNotification = notification;

            if (ShouldFail)
            {
                throw new InvalidOperationException(
                    "Simulated notification delivery failure.");
            }

            return Task.CompletedTask;
        }
    }

    private static WayhunterSharedDbContext CreateDb()
    {
        var options =
            new DbContextOptionsBuilder<WayhunterSharedDbContext>()
                .UseInMemoryDatabase(
                    $"m3-outbox-{Guid.NewGuid():N}")
                .Options;

        return new WayhunterSharedDbContext(options);
    }

    private static TripMembershipRevocationOutboxRow CreatePending()
    {
        return new TripMembershipRevocationOutboxRow
        {
            Id = Guid.NewGuid(),
            TripPlanId = Guid.NewGuid(),
            MemberId = Guid.NewGuid(),
            MemberUserId = "removed-member",
            RemovedAtUtc = Now.AddMinutes(-2),
            CreatedAtUtc = Now.AddMinutes(-1),
            IsDelivered = false,
            DeliveredAtUtc = null,
            DeliveryAttempts = 0
        };
    }

    [Fact]
    public async Task EmptyOutbox_DoesNotSend()
    {
        await using var db = CreateDb();

        var sender = new FakeSender();

        var processor =
            new TripMembershipRevocationOutboxProcessor(
                db,
                sender,
                new FixedClock());

        var result = await processor.ProcessNextAsync();

        Assert.False(result);
        Assert.Equal(0, sender.Calls);
    }

    [Fact]
    public async Task SuccessfulSend_MarksDelivered()
    {
        await using var db = CreateDb();

        var pending = CreatePending();

        db.TripMembershipRevocationOutbox.Add(pending);
        await db.SaveChangesAsync();

        var sender = new FakeSender();

        var processor =
            new TripMembershipRevocationOutboxProcessor(
                db,
                sender,
                new FixedClock());

        var result = await processor.ProcessNextAsync();

        Assert.True(result);
        Assert.Equal(1, sender.Calls);

        Assert.NotNull(sender.LastNotification);
        Assert.Equal(
            pending.Id,
            sender.LastNotification!.NotificationId);

        Assert.True(pending.IsDelivered);
        Assert.Equal(1, pending.DeliveryAttempts);
        Assert.Equal(Now, pending.DeliveredAtUtc);
    }

    [Fact]
    public async Task FailedSend_RemainsPending()
    {
        await using var db = CreateDb();

        var pending = CreatePending();

        db.TripMembershipRevocationOutbox.Add(pending);
        await db.SaveChangesAsync();

        var sender = new FakeSender
        {
            ShouldFail = true
        };

        var processor =
            new TripMembershipRevocationOutboxProcessor(
                db,
                sender,
                new FixedClock());

        await Assert.ThrowsAsync<
            InvalidOperationException>(
            () => processor.ProcessNextAsync());

        Assert.False(pending.IsDelivered);
        Assert.Null(pending.DeliveredAtUtc);
        Assert.Equal(1, pending.DeliveryAttempts);
    }

    [Fact]
    public async Task FailedSend_CanRetrySuccessfully()
    {
        await using var db = CreateDb();

        var pending = CreatePending();

        db.TripMembershipRevocationOutbox.Add(pending);
        await db.SaveChangesAsync();

        var sender = new FakeSender
        {
            ShouldFail = true
        };

        var processor =
            new TripMembershipRevocationOutboxProcessor(
                db,
                sender,
                new FixedClock());

        await Assert.ThrowsAsync<
            InvalidOperationException>(
            () => processor.ProcessNextAsync());

        sender.ShouldFail = false;

        Assert.True(await processor.ProcessNextAsync());

        Assert.True(pending.IsDelivered);
        Assert.Equal(2, pending.DeliveryAttempts);
        Assert.Equal(2, sender.Calls);
    }

    [Fact]
    public async Task DeliveredEvent_IsNotSentAgain()
    {
        await using var db = CreateDb();

        var pending = CreatePending();

        db.TripMembershipRevocationOutbox.Add(pending);
        await db.SaveChangesAsync();

        var sender = new FakeSender();

        var processor =
            new TripMembershipRevocationOutboxProcessor(
                db,
                sender,
                new FixedClock());

        Assert.True(await processor.ProcessNextAsync());
        Assert.False(await processor.ProcessNextAsync());

        Assert.Equal(1, sender.Calls);
        Assert.Equal(1, pending.DeliveryAttempts);
    }
}
