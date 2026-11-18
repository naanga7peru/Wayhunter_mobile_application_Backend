
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

public sealed class ReorderTripStopsServiceTests
{
    private static readonly byte[] ValidVersion =
        { 1, 2, 3, 4, 5, 6, 7, 8 };

    private static TripPlan CreateTrip()
    {
        var trip = new TripPlan(
            "user-001",
            "Jaffna Tour",
            new DateOnly(2026, 11, 20),
            new DateOnly(2026, 11, 22),
            15000m,
            "Car",
            3);

        trip.AddStop("Jaffna Fort");
        trip.AddStop("Nallur Temple");
        trip.AddStop("Point Pedro");

        return trip;
    }

    private static void SetRowVersion(TripPlan trip)
    {
        typeof(TripPlan)
            .GetProperty(
                nameof(TripPlan.RowVersion),
                BindingFlags.Instance | BindingFlags.Public)!
            .SetValue(trip, ValidVersion.ToArray());
    }

    // ==========================================
    // TEST 01 - VALID REORDER
    // ==========================================
    [Fact]
    public async Task ValidReorder_UpdatesStopOrder()
    {
        var repo = new FakeRepository();
        var trip = CreateTrip();
        await repo.AddAsync(trip);

        var order = new[]
        {
            trip.Stops[2].Id,
            trip.Stops[0].Id,
            trip.Stops[1].Id
        };

        var service = new ReorderTripStopsService(repo);

        var result = await service.ReorderAsync(
            trip.Id,
            new ReorderTripStopsDto(order),
            "user-001");

        Assert.NotNull(result);
        Assert.Equal(3, result.Stops.Count);

        Assert.Equal("Point Pedro", result.Stops[0].Name);
        Assert.Equal("Jaffna Fort", result.Stops[1].Name);
        Assert.Equal("Nallur Temple", result.Stops[2].Name);

        Assert.Equal(
            new[] { 0, 1, 2 },
            result.Stops.Select(s => s.OrderIndex));

        Assert.Equal(1, repo.UpdateCount);
    }

    // ==========================================
    // TEST 02 - UNCHANGED ORDER
    // ==========================================
    [Fact]
    public async Task SameOrder_DoesNotWriteAgain()
    {
        var repo = new FakeRepository();
        var trip = CreateTrip();
        await repo.AddAsync(trip);

        var order = trip.Stops
            .Select(stop => stop.Id)
            .ToArray();

        var service = new ReorderTripStopsService(repo);

        var result = await service.ReorderAsync(
            trip.Id,
            new ReorderTripStopsDto(order),
            "user-001");

        Assert.NotNull(result);
        Assert.Equal(3, result.Stops.Count);
        Assert.Equal(0, repo.UpdateCount);
    }

    // ==========================================
    // TEST 03 - DIFFERENT OWNER
    // ==========================================
    [Fact]
    public async Task DifferentOwner_ReturnsNull()
    {
        var repo = new FakeRepository();
        var trip = CreateTrip();
        await repo.AddAsync(trip);

        var service = new ReorderTripStopsService(repo);

        var result = await service.ReorderAsync(
            trip.Id,
            new ReorderTripStopsDto(
                trip.Stops.Select(s => s.Id).Reverse().ToArray()),
            "user-002");

        Assert.Null(result);
        Assert.Equal(0, repo.UpdateCount);
    }

    // ==========================================
    // TEST 04 - UNKNOWN TRIP
    // ==========================================
    [Fact]
    public async Task UnknownTrip_ReturnsNull()
    {
        var repo = new FakeRepository();
        var service = new ReorderTripStopsService(repo);

        var result = await service.ReorderAsync(
            Guid.NewGuid(),
            new ReorderTripStopsDto(Array.Empty<Guid>()),
            "user-001");

        Assert.Null(result);
    }

    // ==========================================
    // TEST 05 - CANCELLED TRIP
    // ==========================================
    [Fact]
    public async Task CancelledTrip_IsRejected()
    {
        var repo = new FakeRepository();
        var trip = CreateTrip();

        trip.Cancel();
        await repo.AddAsync(trip);

        var service = new ReorderTripStopsService(repo);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ReorderAsync(
                trip.Id,
                new ReorderTripStopsDto(
                    trip.Stops.Select(s => s.Id).ToArray()),
                "user-001"));

        Assert.Equal(0, repo.UpdateCount);
    }

    // ==========================================
    // TEST 06 - DUPLICATE STOP IDS
    // ==========================================
    [Fact]
    public async Task DuplicateStopIds_AreRejected()
    {
        var repo = new FakeRepository();
        var trip = CreateTrip();
        await repo.AddAsync(trip);

        var request = new ReorderTripStopsDto(
            new[]
            {
                trip.Stops[0].Id,
                trip.Stops[0].Id,
                trip.Stops[2].Id
            });

        var service = new ReorderTripStopsService(repo);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.ReorderAsync(
                trip.Id, request, "user-001"));

        Assert.Equal(0, repo.UpdateCount);
        Assert.Equal("Nallur Temple", trip.Stops[1].Name);
    }

    // ==========================================
    // TEST 07 - MISSING STOP
    // ==========================================
    [Fact]
    public async Task MissingStopId_IsRejected()
    {
        var repo = new FakeRepository();
        var trip = CreateTrip();
        await repo.AddAsync(trip);

        var service = new ReorderTripStopsService(repo);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.ReorderAsync(
                trip.Id,
                new ReorderTripStopsDto(
                    new[]
                    {
                        trip.Stops[0].Id,
                        trip.Stops[1].Id
                    }),
                "user-001"));

        Assert.Equal(0, repo.UpdateCount);
    }

    // ==========================================
    // TEST 08 - FOREIGN STOP
    // ==========================================
    [Fact]
    public async Task ForeignStopId_IsRejected()
    {
        var repo = new FakeRepository();
        var trip = CreateTrip();
        await repo.AddAsync(trip);

        var service = new ReorderTripStopsService(repo);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.ReorderAsync(
                trip.Id,
                new ReorderTripStopsDto(
                    new[]
                    {
                        trip.Stops[0].Id,
                        trip.Stops[1].Id,
                        Guid.NewGuid()
                    }),
                "user-001"));

        Assert.Equal(0, repo.UpdateCount);
    }

    // ==========================================
    // TEST 09 - STALE ROWVERSION
    // ==========================================
    [Fact]
    public async Task StaleRowVersion_IsRejected()
    {
        var repo = new FakeRepository();
        var trip = CreateTrip();

        SetRowVersion(trip);
        await repo.AddAsync(trip);

        var service = new ReorderTripStopsService(repo);

        await Assert.ThrowsAsync<TripPlanConcurrencyException>(
            () => service.ReorderAsync(
                trip.Id,
                new ReorderTripStopsDto(
                    trip.Stops.Select(s => s.Id).Reverse().ToArray(),
                    new byte[] { 8, 7, 6, 5, 4, 3, 2, 1 }),
                "user-001"));

        Assert.Equal(0, repo.UpdateCount);
    }

    // ==========================================
    // TEST 10 - MISSING SQL ROWVERSION
    // ==========================================
    [Fact]
    public async Task MissingSqlRowVersion_IsRejected()
    {
        var repo = new FakeRepository();
        var trip = CreateTrip();

        SetRowVersion(trip);
        await repo.AddAsync(trip);

        var service = new ReorderTripStopsService(repo);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.ReorderAsync(
                trip.Id,
                new ReorderTripStopsDto(
                    trip.Stops.Select(s => s.Id).Reverse().ToArray()),
                "user-001"));

        Assert.Equal(0, repo.UpdateCount);
    }

    // ==========================================
    // TEST 11 - VALID SQL ROWVERSION
    // ==========================================
    [Fact]
    public async Task ValidRowVersion_ReordersStops()
    {
        var repo = new FakeRepository();
        var trip = CreateTrip();

        SetRowVersion(trip);
        await repo.AddAsync(trip);

        var order = new[]
        {
            trip.Stops[1].Id,
            trip.Stops[2].Id,
            trip.Stops[0].Id
        };

        var service = new ReorderTripStopsService(repo);

        var result = await service.ReorderAsync(
            trip.Id,
            new ReorderTripStopsDto(
                order,
                ValidVersion.ToArray()),
            "user-001");

        Assert.NotNull(result);

        Assert.Equal("Nallur Temple", result.Stops[0].Name);
        Assert.Equal("Point Pedro", result.Stops[1].Name);
        Assert.Equal("Jaffna Fort", result.Stops[2].Name);

        Assert.Equal(1, repo.UpdateCount);
    }

    // ==========================================
    // TEST 12 - EMPTY STOP LIST
    // ==========================================
    [Fact]
    public async Task EmptyTrip_EmptyOrder_DoesNotWrite()
    {
        var repo = new FakeRepository();

        var trip = new TripPlan(
            "user-001",
            "Empty Trip",
            new DateOnly(2026, 11, 20),
            new DateOnly(2026, 11, 21),
            1000m,
            "Car",
            1);

        await repo.AddAsync(trip);

        var service = new ReorderTripStopsService(repo);

        var result = await service.ReorderAsync(
            trip.Id,
            new ReorderTripStopsDto(Array.Empty<Guid>()),
            "user-001");

        Assert.NotNull(result);
        Assert.Empty(result.Stops);
        Assert.Equal(0, repo.UpdateCount);
    }

    // ==========================================
    // FAKE REPOSITORY FOR UNIT TESTS
    // ==========================================
    private sealed class FakeRepository : ITripPlanRepository
    {
        private readonly Dictionary<Guid, TripPlan> _data = new();

        public int UpdateCount { get; private set; }

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
    }
}
