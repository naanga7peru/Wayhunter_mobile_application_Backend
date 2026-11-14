
using System;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.TripSafety.Interfaces;
using Wayhunter.Application.Modules.TripSafety.Services;
using Wayhunter.Domain.Modules.TripSafety.Enums;
using Wayhunter.Infrastructure.Persistence.Repositories.TripSafety;
using Xunit;

namespace Wayhunter.UnitTests.Modules.TripSafety;

public sealed class TripStartReminderDispatchServiceTests
{
    private sealed class FakeReminderSender
        : ITripStartReminderSender
    {
        public int SendCount { get; private set; }

        public string? LastTripPlanId { get; private set; }

        public string? LastRecipientUserId { get; private set; }

        public CancellationToken LastToken { get; private set; }

        public bool ShouldFail { get; set; }

        public Task SendAsync(
            string tripPlanId,
            string recipientUserId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (ShouldFail)
            {
                throw new InvalidOperationException(
                    "Notification sender failed.");
            }

            SendCount++;
            LastTripPlanId = tripPlanId;
            LastRecipientUserId = recipientUserId;
            LastToken = cancellationToken;

            return Task.CompletedTask;
        }
    }

    [Fact]
    public void Null_Sender_Should_Fail()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new TripStartReminderDispatchService(null!));
    }

    [Fact]
    public async Task Valid_Reminder_Should_Send_Notification()
    {
        var sender = new FakeReminderSender();

        var service = new TripStartReminderDispatchService(
            sender);

        await service.DispatchAsync(
            "plan-001",
            "user-001");

        Assert.Equal(1, sender.SendCount);
        Assert.Equal("plan-001", sender.LastTripPlanId);
        Assert.Equal("user-001", sender.LastRecipientUserId);
    }

    [Fact]
    public async Task Reminder_Should_Trim_Ids()
    {
        var sender = new FakeReminderSender();

        var service = new TripStartReminderDispatchService(
            sender);

        await service.DispatchAsync(
            "  plan-001  ",
            "  user-001  ");

        Assert.Equal("plan-001", sender.LastTripPlanId);
        Assert.Equal("user-001", sender.LastRecipientUserId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Missing_TripPlanId_Should_Fail(
        string? tripPlanId)
    {
        var sender = new FakeReminderSender();

        var service = new TripStartReminderDispatchService(
            sender);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.DispatchAsync(
                tripPlanId,
                "user-001"));

        Assert.Equal(0, sender.SendCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Missing_Recipient_Should_Fail(
        string? recipientUserId)
    {
        var sender = new FakeReminderSender();

        var service = new TripStartReminderDispatchService(
            sender);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.DispatchAsync(
                "plan-001",
                recipientUserId));

        Assert.Equal(0, sender.SendCount);
    }

    [Fact]
    public async Task Reminder_Must_Not_Start_Trip()
    {
        var repository =
            new InMemoryLiveTripSessionRepository();

        var sender = new FakeReminderSender();

        var reminderService =
            new TripStartReminderDispatchService(sender);

        // No trip session exists before reminder.
        var before =
            await repository.GetOpenByTripPlanIdAsync(
                "plan-001");

        Assert.Null(before);

        await reminderService.DispatchAsync(
            "plan-001",
            "user-001");

        // Notification dispatch must not create
        // an active trip session.
        var after =
            await repository.GetOpenByTripPlanIdAsync(
                "plan-001");

        Assert.Null(after);
        Assert.Equal(1, sender.SendCount);
    }

    [Fact]
    public async Task Reminder_Must_Not_Resume_Paused_Trip()
    {
        var repository =
            new InMemoryLiveTripSessionRepository();

        var tripService = new LiveTripSessionService(
            repository,
            TimeProvider.System);

        var session =
            await tripService.StartAsync("plan-001");

        await tripService.PauseAsync(session.SessionId);

        var sender = new FakeReminderSender();

        var reminderService =
            new TripStartReminderDispatchService(sender);

        await reminderService.DispatchAsync(
            "plan-001",
            "user-001");

        var stored =
            await repository.GetByIdAsync(session.SessionId);

        Assert.NotNull(stored);
        Assert.Same(session, stored);
        Assert.Equal(LiveTripStatus.Paused, stored.Status);
        Assert.Equal(1, sender.SendCount);
    }

    [Fact]
    public async Task Cancelled_Dispatch_Should_Not_Send()
    {
        var sender = new FakeReminderSender();

        var service = new TripStartReminderDispatchService(
            sender);

        using var cts = new CancellationTokenSource();

        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.DispatchAsync(
                "plan-001",
                "user-001",
                cts.Token));

        Assert.Equal(0, sender.SendCount);
    }

    [Fact]
    public async Task Sender_Failure_Should_Propagate()
    {
        var sender = new FakeReminderSender
        {
            ShouldFail = true
        };

        var service = new TripStartReminderDispatchService(
            sender);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.DispatchAsync(
                "plan-001",
                "user-001"));

        Assert.Equal(0, sender.SendCount);
    }

    [Fact]
    public async Task CancellationToken_Should_Be_Forwarded()
    {
        var sender = new FakeReminderSender();

        var service = new TripStartReminderDispatchService(
            sender);

        using var cts = new CancellationTokenSource();

        await service.DispatchAsync(
            "plan-001",
            "user-001",
            cts.Token);

        Assert.Equal(cts.Token, sender.LastToken);
        Assert.Equal(1, sender.SendCount);
    }
}
