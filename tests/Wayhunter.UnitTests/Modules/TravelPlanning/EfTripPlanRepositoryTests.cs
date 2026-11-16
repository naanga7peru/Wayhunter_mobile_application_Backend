
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Wayhunter.Application.Modules.TravelPlanning.Exceptions;
using Wayhunter.Domain.Modules.TravelPlanning.Entities;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Wayhunter.Infrastructure.Persistence.Repositories.TravelPlanning;
using Xunit;

namespace Wayhunter.UnitTests.Modules.TravelPlanning;

public sealed class EfTripPlanRepositoryTests
{
    private static DbContextOptions<WayhunterSharedDbContext>
        CreateOptions(string databaseName)
    {
        return new DbContextOptionsBuilder<WayhunterSharedDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
    }

    private static TripPlan CreateTrip(
        string ownerUserId = "user-001")
    {
        return new TripPlan(
            ownerUserId,
            "Jaffna Travel Plan",
            new DateOnly(2026, 11, 20),
            new DateOnly(2026, 11, 22),
            15000m,
            "Car",
            3);
    }

    private static byte[] CreateRowVersion(byte seed = 1)
    {
        return Enumerable.Range(0, 8)
            .Select(index => (byte)(seed + index))
            .ToArray();
    }

    private static void SetRowVersion(
        TripPlan trip,
        byte[] rowVersion)
    {
        var property = typeof(TripPlan).GetProperty(
            nameof(TripPlan.RowVersion),
            BindingFlags.Instance | BindingFlags.Public);

        Assert.NotNull(property);

        property.SetValue(trip, rowVersion.ToArray());
    }

    [Fact]
    public void Constructor_NullContext_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            () => new EfTripPlanRepository(null!));
    }

    [Fact]
    public async Task AddAndGetById_ShouldPersistTripAndStops()
    {
        var options = CreateOptions(Guid.NewGuid().ToString());

        await using var db = new WayhunterSharedDbContext(options);

        var repository = new EfTripPlanRepository(db);

        var trip = CreateTrip();

        trip.AddStop(
            "Jaffna Fort",
            latitude: 9.6615,
            longitude: 80.0255);

        await repository.AddAsync(trip);

        var result = await repository.GetByIdAsync(trip.Id);

        Assert.NotNull(result);
        Assert.Equal(trip.Id, result.Id);
        Assert.Equal("Jaffna Travel Plan", result.TripName);

        var stop = Assert.Single(result.Stops);

        Assert.Equal("Jaffna Fort", stop.Name);
        Assert.Equal(9.6615, stop.Latitude);
        Assert.Equal(80.0255, stop.Longitude);
    }

    [Fact]
    public async Task GetByOwner_ShouldReturnOnlyMatchingOwnerTrips()
    {
        var options = CreateOptions(Guid.NewGuid().ToString());

        await using var db = new WayhunterSharedDbContext(options);

        var repository = new EfTripPlanRepository(db);

        var ownerTrip = CreateTrip("user-001");
        var otherTrip = CreateTrip("user-002");

        await repository.AddAsync(ownerTrip);
        await repository.AddAsync(otherTrip);

        var results = await repository.GetByOwnerAsync("user-001");

        var result = Assert.Single(results);

        Assert.Equal(ownerTrip.Id, result.Id);
        Assert.Equal("user-001", result.OwnerUserId);
    }

    [Fact]
    public async Task UpdateAsync_ShouldPersistChangedTrip()
    {
        var options = CreateOptions(Guid.NewGuid().ToString());

        await using var db = new WayhunterSharedDbContext(options);

        var repository = new EfTripPlanRepository(db);

        var trip = CreateTrip();

        await repository.AddAsync(trip);

        trip.UpdateDetails(
            "Updated Jaffna Plan",
            new DateOnly(2026, 11, 21),
            new DateOnly(2026, 11, 23),
            20000m,
            "Van",
            5);

        await repository.UpdateAsync(trip);

        var result = await repository.GetByIdAsync(trip.Id);

        Assert.NotNull(result);
        Assert.Equal("Updated Jaffna Plan", result.TripName);
        Assert.Equal(20000m, result.BudgetLkr);
        Assert.Equal("Van", result.VehicleType);
        Assert.Equal(5, result.Headcount);
    }

    [Fact]
    public async Task UpdateWithConcurrencyAsync_InvalidToken_ShouldReject()
    {
        var options = CreateOptions(Guid.NewGuid().ToString());

        await using var db = new WayhunterSharedDbContext(options);

        var repository = new EfTripPlanRepository(db);

        var trip = CreateTrip();

        await repository.AddAsync(trip);

        var updatedTrip = await repository.GetByIdAsync(trip.Id);

        Assert.NotNull(updatedTrip);

        await Assert.ThrowsAsync<ArgumentException>(
            () => repository.UpdateWithConcurrencyAsync(
                updatedTrip,
                new byte[] { 1, 2, 3 }));
    }

    [Fact]
    public async Task UpdateWithConcurrencyAsync_StaleDatabaseToken_ShouldThrow()
    {
        var options = CreateOptions(Guid.NewGuid().ToString());

        var originalVersion = CreateRowVersion(1);
        var changedVersion = CreateRowVersion(20);

        var trip = CreateTrip();

        SetRowVersion(trip, originalVersion);

        // Seed initial database record.
        await using (var seedDb =
            new WayhunterSharedDbContext(options))
        {
            await seedDb.Trips.AddAsync(trip);
            await seedDb.SaveChangesAsync();
        }

        // First request loads the original version.
        await using var firstDb =
            new WayhunterSharedDbContext(options);

        var repository = new EfTripPlanRepository(firstDb);

        var firstTrip = await repository.GetByIdAsync(trip.Id);

        Assert.NotNull(firstTrip);

        // Second request changes the stored version.
        await using (var secondDb =
            new WayhunterSharedDbContext(options))
        {
            var secondTrip = await secondDb.Trips
                .SingleAsync(item => item.Id == trip.Id);

            SetRowVersion(secondTrip, changedVersion);

            await secondDb.SaveChangesAsync();
        }

        firstTrip.UpdateDetails(
            "Attempted stale update",
            new DateOnly(2026, 11, 21),
            new DateOnly(2026, 11, 24),
            25000m,
            "Bus",
            4);

        await Assert.ThrowsAsync<TripPlanConcurrencyException>(
            () => repository.UpdateWithConcurrencyAsync(
                firstTrip,
                originalVersion));
    }
}
