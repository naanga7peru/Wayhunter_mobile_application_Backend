using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wayhunter.Api.Extensions;
using Wayhunter.Api.Services.TripSafety;
using Wayhunter.Application.Modules.TripSafety.Interfaces;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Wayhunter.Infrastructure.Persistence.Repositories.TripSafety;
using Xunit;

namespace Wayhunter.UnitTests.Modules.TripSafety;

public sealed class M3SafeCheckInCompletionTests
{
    [Fact]
    public async Task StoreRejectsUntrustedUserBeforeResolvingDatabase()
    {
        var store = new EfTripSafeCheckInStore(new UnexpectedContextFactory());
        var checkIn = new TripSafeCheckIn(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "spoofed", DateTimeOffset.UtcNow);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => store.RecordAsync(checkIn));
    }

    [Fact]
    public async Task StoreAndDispatcherRejectInMemoryProvider()
    {
        var factory = new InMemoryContextFactory();
        var store = new EfTripSafeCheckInStore(factory);
        var checkIn = new TripSafeCheckIn(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid().ToString("D"), DateTimeOffset.UtcNow);

        await Assert.ThrowsAsync<TripSafetyCheckInUnavailableException>(() => store.RecordAsync(checkIn));
        await using var db = factory.CreateDbContext();
        Assert.Throws<InvalidOperationException>(() =>
            new TripSafeCheckInOutboxDispatcher(db, new UnexpectedSender(), TimeProvider.System, new M3OutboxWorkerSettings()));
    }

    [Fact]
    public void ProductionRegistrationKeepsSqlStoreAndOwnerDeliveryDisabled()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["TripSafety:EnableSqlPersistence"] = "true",
            ["TripSafety:EnableSafeCheckIn"] = "true"
        }).Build();
        var services = new ServiceCollection().AddTripSafetyBatch2Services(config);

        var store = Assert.Single(services, x => x.ServiceType == typeof(ITripSafetyCheckInStore));
        Assert.Equal(typeof(UnavailableTripSafetyCheckInStore), store.ImplementationType);
        Assert.DoesNotContain(services, x => x.ServiceType == typeof(TripSafeCheckInOutboxDispatcher) ||
            x.ServiceType == typeof(ITripSafeCheckInOwnerNotificationSender) ||
            (x.ServiceType == typeof(IHostedService) && x.ImplementationType == typeof(TripSafeCheckInOutboxWorker)));
    }

    private sealed class UnexpectedContextFactory : IDbContextFactory<WayhunterSharedDbContext>
    {
        public WayhunterSharedDbContext CreateDbContext() =>
            throw new InvalidOperationException("Invalid identity reached persistence.");
    }

    private sealed class InMemoryContextFactory : IDbContextFactory<WayhunterSharedDbContext>
    {
        public WayhunterSharedDbContext CreateDbContext() => new(
            new DbContextOptionsBuilder<WayhunterSharedDbContext>().UseInMemoryDatabase("disabled-check-in").Options);
    }

    private sealed class UnexpectedSender : ITripSafeCheckInOwnerNotificationSender
    {
        public Task SendAsync(TripSafeCheckInOwnerNotification notification, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("In-memory delivery is unavailable.");
    }
}
