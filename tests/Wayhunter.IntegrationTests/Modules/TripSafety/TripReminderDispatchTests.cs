using Microsoft.EntityFrameworkCore;
using Wayhunter.Application.Modules.TripSafety.Interfaces;
using Wayhunter.Application.Modules.TripSafety.Services;
using Wayhunter.Domain.Modules.TripSafety.Entities;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Wayhunter.Infrastructure.Persistence.Repositories.TripSafety;
using Xunit;

namespace Wayhunter.IntegrationTests.Modules.TripSafety;

public sealed class TripReminderDispatchTests
{
    [Fact]
    public async Task Dispatch_RequiresTheExactPersistedOpenSession()
    {
        var options = new DbContextOptionsBuilder<WayhunterSharedDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var tripId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var earlier = LiveTripSession.Start(Guid.NewGuid(), tripId, now.AddHours(-2));
        earlier.Finish(now.AddHours(-1));
        var current = LiveTripSession.Start(Guid.NewGuid(), tripId, now);
        var other = LiveTripSession.Start(Guid.NewGuid(), Guid.NewGuid(), now);

        await using var writer = new WayhunterSharedDbContext(options);
        writer.LiveTripSessions.AddRange(earlier, current, other);
        await writer.SaveChangesAsync();

        await using var reader = new WayhunterSharedDbContext(options);
        var sender = new RecordingSender();
        var dispatch = new TripStartReminderDispatchService(sender, new EfTripFinishedReminderGuard(reader));

        await dispatch.DispatchAsync(tripId, earlier.SessionId, "recipient");
        await dispatch.DispatchAsync(tripId, Guid.NewGuid(), "recipient");
        await dispatch.DispatchAsync(tripId, other.SessionId, "recipient");
        Assert.Empty(sender.Deliveries);

        await dispatch.DispatchAsync(tripId, current.SessionId, "recipient");
        current.Pause();
        await writer.SaveChangesAsync();
        await dispatch.DispatchAsync(tripId, current.SessionId, "recipient");

        Assert.Equal(new[] { (tripId, current.SessionId), (tripId, current.SessionId) }, sender.Deliveries);

        current.Finish(now.AddMinutes(1));
        await writer.SaveChangesAsync();
        await dispatch.DispatchAsync(tripId, current.SessionId, "recipient");
        await dispatch.DispatchAsync(tripId, current.SessionId, "recipient");

        Assert.Equal(2, sender.Deliveries.Count);
    }

    private sealed class RecordingSender : ITripStartReminderSender
    {
        public List<(Guid TripPlanId, Guid SessionId)> Deliveries { get; } = [];

        public Task SendAsync(Guid tripPlanId, Guid sessionId, string recipientUserId,
            CancellationToken cancellationToken = default)
        {
            Deliveries.Add((tripPlanId, sessionId));
            return Task.CompletedTask;
        }
    }
}
