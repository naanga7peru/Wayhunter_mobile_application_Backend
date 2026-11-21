using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Wayhunter.Application.Modules.TripSafety.Memories;
using Wayhunter.Domain.Modules.TravelPlanning.Entities;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Wayhunter.Infrastructure.Persistence.Repositories.TripSafety;
using Xunit;

namespace Wayhunter.IntegrationTests.Modules.TripSafety;

public sealed class M3Ms07AdditionalSafetyTests
{
    [Fact]
    public async Task CrossTripStopCannotBeAttachedToMemory()
    {
        var factory = new TestFactory();

        var owner = Guid.NewGuid().ToString("D");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var firstTrip = new TripPlan(
            owner,
            "First trip",
            today,
            today.AddDays(1),
            100,
            "Car",
            2);

        var secondTrip = new TripPlan(
            owner,
            "Second trip",
            today,
            today.AddDays(1),
            100,
            "Car",
            2);

        var otherTripStop = secondTrip.AddStop("Other trip stop");

        await using (var db = factory.CreateDbContext())
        {
            db.Trips.Add(firstTrip);
            db.Trips.Add(secondTrip);

            await db.SaveChangesAsync();
        }

        var store = new EfTripMemoryStore(
            factory,
            TimeProvider.System);

        var now = DateTimeOffset.UtcNow;

        var candidate = new TripMemory(
            Guid.NewGuid(),
            firstTrip.Id,
            otherTripStop.Id,
            owner,
            Guid.NewGuid(),
            "Cross-trip memory",
            TripMemoryVisibility.Private,
            now,
            now);

        await Assert.ThrowsAsync<TripMemoryConflictException>(
            () => store.CreateAsync(
                candidate,
                CancellationToken.None));

        await using var verify = factory.CreateDbContext();

        Assert.False(
            await verify.TripMemories.AnyAsync(
                row => row.MemoryId == candidate.MemoryId));
    }

    [Fact]
    public async Task SoftDeletedMemoryRetainsStopReferenceAndOperationIdentity()
    {
        var factory = new TestFactory();

        var owner = Guid.NewGuid().ToString("D");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var trip = new TripPlan(
            owner,
            "Tombstone trip",
            today,
            today.AddDays(1),
            100,
            "Car",
            2);

        var stop = trip.AddStop("Referenced stop");

        await using (var db = factory.CreateDbContext())
        {
            db.Trips.Add(trip);

            await db.SaveChangesAsync();
        }

        var store = new EfTripMemoryStore(
            factory,
            TimeProvider.System);

        var now = DateTimeOffset.UtcNow;

        var memory = new TripMemory(
            Guid.NewGuid(),
            trip.Id,
            stop.Id,
            owner,
            Guid.NewGuid(),
            "Retained reference",
            TripMemoryVisibility.Private,
            now,
            now);

        var created = await store.CreateAsync(
            memory,
            CancellationToken.None);

        Assert.True(created.Created);

        var deleted = await store.SoftDeleteAsync(
            trip.Id,
            memory.MemoryId,
            owner,
            now.AddMinutes(1),
            CancellationToken.None);

        Assert.True(deleted);

        Assert.Null(
            await store.FindAsync(
                trip.Id,
                memory.MemoryId,
                CancellationToken.None));

        await using (var verify = factory.CreateDbContext())
        {
            var retained = await verify.TripMemories.SingleAsync(
                row => row.MemoryId == memory.MemoryId);

            Assert.Equal(stop.Id, retained.TripStopId);
            Assert.Equal(memory.CreateOperationId, retained.CreateOperationId);
            Assert.NotNull(retained.DeletedAtUtc);
        }

        await Assert.ThrowsAsync<TripMemoryConflictException>(
            () => store.CreateAsync(
                memory with { MemoryId = Guid.NewGuid() },
                CancellationToken.None));
    }

    private sealed class TestFactory
        : IDbContextFactory<WayhunterSharedDbContext>
    {
        private readonly DbContextOptions<WayhunterSharedDbContext>
            _options =
                new DbContextOptionsBuilder<WayhunterSharedDbContext>()
                    .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                    .ConfigureWarnings(warnings =>
                        warnings.Ignore(
                            InMemoryEventId.TransactionIgnoredWarning))
                    .Options;

        public WayhunterSharedDbContext CreateDbContext()
        {
            return new WayhunterSharedDbContext(_options);
        }
    }
}
