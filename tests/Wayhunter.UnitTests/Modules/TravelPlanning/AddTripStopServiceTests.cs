
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.TravelPlanning.Commands;
using Wayhunter.Application.Modules.TravelPlanning.Dtos;
using Wayhunter.Application.Modules.TravelPlanning.Exceptions;
using Wayhunter.Application.Modules.TravelPlanning.Interfaces;
using Wayhunter.Domain.Modules.TravelPlanning.Entities;
using Xunit;

namespace Wayhunter.UnitTests.Modules.TravelPlanning;

public sealed class AddTripStopServiceTests
{
    private static readonly byte[] ValidRowVersion =
        { 1, 2, 3, 4, 5, 6, 7, 8 };

    private static TripPlan CreateTrip(
        string owner = "user-001")
    {
        return new TripPlan(
            owner,
            "Jaffna Tour",
            new DateOnly(2026, 11, 20),
            new DateOnly(2026, 11, 22),
            15000m,
            "Car",
            3);
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
    public async Task AddStop_ValidOwner_AddsStop()
    {
        var repo = new FakeRepository();
        var trip = CreateTrip();
        await repo.AddAsync(trip);

        var service = new AddTripStopService(repo);

        var result = await service.AddAsync(
            trip.Id,
            new AddTripStopDto(
                "Jaffna Fort",
                "place-001",
                9.662,
                80.009),
            "user-001");

        Assert.NotNull(result);

        var stop = Assert.Single(result.Stops);
        Assert.Equal("Jaffna Fort", stop.Name);
        Assert.Equal("place-001", stop.PlaceId);
        Assert.Equal(0, stop.OrderIndex);
        Assert.Equal(1, repo.UpdateCount);
    }

    [Fact]
    public async Task AddStop_DifferentOwner_ReturnsNull()
    {
        var repo = new FakeRepository();
        var trip = CreateTrip();
        await repo.AddAsync(trip);

        var service = new AddTripStopService(repo);

        var result = await service.AddAsync(
            trip.Id,
            new AddTripStopDto("Jaffna Fort"),
            "user-002");

        Assert.Null(result);
        Assert.Empty(trip.Stops);
        Assert.Equal(0, repo.UpdateCount);
    }

    [Fact]
    public async Task AddStop_UnknownTrip_ReturnsNull()
    {
        var repo = new FakeRepository();
        var service = new AddTripStopService(repo);

        var result = await service.AddAsync(
            Guid.NewGuid(),
            new AddTripStopDto("Jaffna Fort"),
            "user-001");

        Assert.Null(result);
    }

    [Fact]
    public async Task AddStop_InvalidCoordinates_Throws()
    {
        var repo = new FakeRepository();
        var trip = CreateTrip();
        await repo.AddAsync(trip);

        var service = new AddTripStopService(repo);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.AddAsync(
                trip.Id,
                new AddTripStopDto(
                    "Jaffna Fort",
                    Latitude: 100,
                    Longitude: 80),
                "user-001"));

        Assert.Empty(trip.Stops);
        Assert.Equal(0, repo.UpdateCount);
    }

    [Fact]
    public async Task AddStop_CancelledTrip_Throws()
    {
        var repo = new FakeRepository();
        var trip = CreateTrip();

        trip.Cancel();
        await repo.AddAsync(trip);

        var service = new AddTripStopService(repo);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.AddAsync(
                trip.Id,
                new AddTripStopDto("Jaffna Fort"),
                "user-001"));

        Assert.Empty(trip.Stops);
    }

    [Fact]
    public async Task AddStop_StaleRowVersion_ThrowsConflict()
    {
        var repo = new FakeRepository();
        var trip = CreateTrip();

        SetRowVersion(trip, ValidRowVersion);
        await repo.AddAsync(trip);

        var service = new AddTripStopService(repo);

        await Assert.ThrowsAsync<TripPlanConcurrencyException>(
            () => service.AddAsync(
                trip.Id,
                new AddTripStopDto(
                    "Jaffna Fort",
                    ExpectedRowVersion:
                        new byte[] { 8, 7, 6, 5, 4, 3, 2, 1 }),
                "user-001"));

        Assert.Empty(trip.Stops);
        Assert.Equal(0, repo.UpdateCount);
    }

    [Fact]
    public async Task AddStop_ValidRowVersion_PersistsStop()
    {
        var repo = new FakeRepository();
        var trip = CreateTrip();

        SetRowVersion(trip, ValidRowVersion);
        await repo.AddAsync(trip);

        var service = new AddTripStopService(repo);

        var result = await service.AddAsync(
            trip.Id,
            new AddTripStopDto(
                "Nallur Temple",
                ExpectedRowVersion:
                    ValidRowVersion.ToArray()),
            "user-001");

        Assert.NotNull(result);
        Assert.Single(result.Stops);
        Assert.Equal("Nallur Temple", result.Stops[0].Name);
        Assert.Equal(1, repo.ConcurrencyUpdateCount);
    }

    private sealed class FakeRepository : ITripPlanRepository
    {
        private readonly Dictionary<Guid, TripPlan> _data = new();

        public int UpdateCount { get; private set; }
        public int ConcurrencyUpdateCount { get; private set; }

        public Task<TripPlan?> GetByIdAsync(
            Guid id,
            CancellationToken ct = default)
        {
            _data.TryGetValue(id, out var trip);
            return Task.FromResult(trip);
        }

        public Task<IReadOnlyList<TripPlan>> GetByOwnerAsync(
            string owner,
            CancellationToken ct = default)
        {
            IReadOnlyList<TripPlan> result =
                _data.Values
                    .Where(x => x.OwnerUserId == owner)
                    .ToList();

            return Task.FromResult(result);
        }

        public Task AddAsync(
            TripPlan trip,
            CancellationToken ct = default)
        {
            _data.Add(trip.Id, trip);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(
            TripPlan trip,
            CancellationToken ct = default)
        {
            _data[trip.Id] = trip;
            UpdateCount++;
            return Task.CompletedTask;
        }

        public Task UpdateWithConcurrencyAsync(
            TripPlan trip,
            byte[] expectedRowVersion,
            CancellationToken ct = default)
        {
            ConcurrencyUpdateCount++;
            _data[trip.Id] = trip;
            return Task.CompletedTask;
        }
    }
}
