using System;
using System.Collections.Generic;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Wayhunter.Api.Extensions;
using Wayhunter.Application.Modules.TripSafety.Interfaces;
using Wayhunter.Application.Modules.TripSafety.Services;
using Wayhunter.Infrastructure.Persistence.Repositories.TripSafety;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Xunit;
namespace Wayhunter.UnitTests.Modules.TripSafety;
public sealed class M3Batch2ReviewSafetyTests
{
    private sealed class Sender : ITripStartReminderSender
    {
        public int Count { get; private set; }
        public Task SendAsync(Guid tripPlanId, Guid sessionId, string recipientUserId, CancellationToken cancellationToken = default) { Count++; return Task.CompletedTask; }
    }
    private sealed class UnavailableSqlConnection : DbConnectionInterceptor
    {
        public override ValueTask<InterceptionResult> ConnectionOpeningAsync(
            DbConnection connection, ConnectionEventData eventData, InterceptionResult result,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Simulated database outage.");
    }

    private sealed class FailingGuard : ITripFinishedReminderGuard
    {
        public Task<bool> CanDispatchAsync(Guid tripPlanId, Guid sessionId, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Persistence unavailable");
    }
    private sealed class FinishedGuard : ITripFinishedReminderGuard
    {
        public Task<bool> CanDispatchAsync(Guid tripPlanId, Guid sessionId, CancellationToken cancellationToken = default) => Task.FromResult(false);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BothRuntimeModes_DisableUnsafeCheckInStore(bool sql)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["TripSafety:EnableSqlPersistence"] = sql.ToString() }).Build();
        var services = new ServiceCollection();
        services.AddTripSafetyBatch2Services(config);
        Assert.Contains(services, x => x.ServiceType == typeof(ITripSafetyCheckInStore) && x.ImplementationType == typeof(UnavailableTripSafetyCheckInStore));
    }
    [Fact]
    public void NonSqlRuntime_UsesUnavailableReminderGuard()
    {
        var services = new ServiceCollection();
        services.AddTripSafetyBatch2Services(new ConfigurationBuilder().Build());
        Assert.Contains(services, x => x.ServiceType == typeof(ITripFinishedReminderGuard) && x.ImplementationType == typeof(UnavailableTripFinishedReminderGuard));
    }
    [Fact]
    public void MissingReminderGuard_PreventsSending()
    {
        var sender = new Sender();
        Assert.Throws<ArgumentNullException>("finishedGuard",
            () => new TripStartReminderDispatchService(sender, null!));
        Assert.Equal(0, sender.Count);
    }
    [Fact]
    public async Task PersistenceFailure_PreventsSending()
    {
        var sender = new Sender();
        var service = new TripStartReminderDispatchService(sender, new FailingGuard());
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DispatchAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid().ToString("D")));
        Assert.Equal(0, sender.Count);
    }
    [Fact]
    public async Task SqlGuard_ConnectionFailure_PreventsSending()
    {
        var options = new DbContextOptionsBuilder<WayhunterSharedDbContext>()
            .UseSqlServer("Server=localhost;Database=UnusedReminderTest;Integrated Security=True")
            .AddInterceptors(new UnavailableSqlConnection())
            .Options;
        await using var context = new WayhunterSharedDbContext(options);
        var sender = new Sender();
        var service = new TripStartReminderDispatchService(sender, new EfTripFinishedReminderGuard(context));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.DispatchAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid().ToString("D")));

        Assert.Equal("Simulated database outage.", error.Message);
        Assert.Equal(0, sender.Count);
    }

    [Fact]
    public async Task FinishedTrip_SuppressesReminder()
    {
        var sender = new Sender();
        var service = new TripStartReminderDispatchService(sender, new FinishedGuard());
        await service.DispatchAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid().ToString("D"));
        Assert.Equal(0, sender.Count);
    }
}
