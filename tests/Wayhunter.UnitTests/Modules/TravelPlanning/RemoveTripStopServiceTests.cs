
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

public sealed class RemoveTripStopServiceTests
{
    private static readonly byte[] Version =
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

    private static void SetVersion(TripPlan trip)
    {
        typeof(TripPlan)
            .GetProperty(
                nameof(TripPlan.RowVersion),
                BindingFlags.Instance | BindingFlags.Public)!
            .SetValue(trip, Version.ToArray());
    }

    [Fact]
    public async Task RemoveMiddleStop_ReordersRemainingStops()
    {
        var repo = new FakeRepository();
        var trip = CreateTrip();
        var stopId = trip.Stops[1].Id;
        await repo.AddAsync(trip);

        var service = new RemoveTripStopService(repo);

        var result = await service.RemoveAsync(
            trip.Id, stopId,
            new RemoveTripStopDto(),
            "user-001");

        Assert.NotNull(result);
        Assert.Equal(2, result.Stops.Count);
        Assert.Equal("Jaffna Fort", result.Stops[0].Name);
        Assert.Equal("Point Pedro", result.Stops[1].Name);
        Assert.Equal(0, result.Stops[0].OrderIndex);
        Assert.Equal(1, result.Stops[1].OrderIndex);
        Assert.Equal(1, repo.UpdateCount);
    }

    [Fact]
    public async Task RemoveFirstStop_ReordersRemainingStops()
    {
        var repo = new FakeRepository();
        var trip = CreateTrip();
        var stopId = trip.Stops[0].Id;
        await repo.AddAsync(trip);

        var service = new RemoveTripStopService(repo);

        var result = await service.RemoveAsync(
            trip.Id, stopId,
            new RemoveTripStopDto(),
            "user-001");

        Assert.NotNull(result);
        Assert.Equal("Nallur Temple", result.Stops[0].Name);
        Assert.Equal("Point Pedro", result.Stops[1].Name);
        Assert.Equal(0, result.Stops[0].OrderIndex);
        Assert.Equal(1, result.Stops[1].OrderIndex);
    }

    [Fact]
    public async Task DifferentOwner_ReturnsNull()
    {
        var repo = new FakeRepository();
        var trip = CreateTrip();
        await repo.AddAsync(trip);

        var service = new RemoveTripStopService(repo);

        var result = await service.RemoveAsync(
            trip.Id, trip.Stops[0].Id,
            new RemoveTripStopDto(),
            "user-002");

        Assert.Null(result);
        Assert.Equal(3, trip.Stops.Count);
        Assert.Equal(0, repo.UpdateCount);
    }

    [Fact]
    public async Task UnknownStop_ReturnsNull()
    {
        var repo = new FakeRepository();
        var trip = CreateTrip();
        await repo.AddAsync(trip);

        var service = new RemoveTripStopService(repo);

        var result = await service.RemoveAsync(
            trip.Id, Guid.NewGuid(),
            new RemoveTripStopDto(),
            "user-001");

        Assert.Null(result);
        Assert.Equal(3, trip.Stops.Count);
    }

    [Fact]
    public async Task CancelledTrip_IsRejected()
    {
        var repo = new FakeRepository();
        var trip = CreateTrip();
        trip.Cancel();
        await repo.AddAsync(trip);

        var service = new RemoveTripStopService(repo);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.RemoveAsync(
                trip.Id, trip.Stops[0].Id,
                new RemoveTripStopDto(),
                "user-001"));

        Assert.Equal(3, trip.Stops.Count);
    }

    [Fact]
    public async Task StaleRowVersion_IsRejected()
    {
        var repo = new FakeRepository();
        var trip = CreateTrip();
        SetVersion(trip);
        await repo.AddAsync(trip);

        var service = new RemoveTripStopService(repo);

        await Assert.ThrowsAsync<TripPlanConcurrencyException>(
            () => service.RemoveAsync(
                trip.Id, trip.Stops[0].Id,
                new RemoveTripStopDto(
                    new byte[] { 8, 7, 6, 5, 4, 3, 2, 1 }),
                "user-001"));

        Assert.Equal(3, trip.Stops.Count);
        Assert.Equal(0, repo.UpdateCount);
    }

    [Fact]
    public async Task ValidRowVersion_RemovesStop()
    {
        var repo = new FakeRepository();
        var trip = CreateTrip();
        SetVersion(trip);
        await repo.AddAsync(trip);

        var service = new RemoveTripStopService(repo);

        var result = await service.RemoveAsync(
            trip.Id, trip.Stops[1].Id,
            new RemoveTripStopDto(Version.ToArray()),
            "user-001");

        Assert.NotNull(result);
        Assert.Equal(2, result.Stops.Count);
        Assert.Equal(1, repo.UpdateCount);
    }

    [Fact]
    public async Task MissingSqlRowVersion_IsRejected()
    {
        var repo = new FakeRepository();
        var trip = CreateTrip();
        SetVersion(trip);
        await repo.AddAsync(trip);

        var service = new RemoveTripStopService(repo);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.RemoveAsync(
                trip.Id, trip.Stops[0].Id,
                new RemoveTripStopDto(),
                "user-001"));

        Assert.Equal(3, trip.Stops.Count);
    }

    private sealed class FakeRepository : ITripPlanRepository
    {
        private readonly Dictionary<Guid, TripPlan> _data = new();

        public int UpdateCount { get; private set; }

        public Task<TripPlan?> GetByIdAsync(
            Guid id, CancellationToken ct = default)
        {
            _data.TryGetValue(id, out var trip);
            return Task.FromResult(trip);
        }

        public Task<IReadOnlyList<TripPlan>> GetByOwnerAsync(
            string owner, CancellationToken ct = default)
        {
            IReadOnlyList<TripPlan> result =
                _data.Values
                    .Where(x => x.OwnerUserId == owner)
                    .ToList();

            return Task.FromResult(result);
        }

        public Task AddAsync(
            TripPlan trip, CancellationToken ct = default)
        {
            _data.Add(trip.Id, trip);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(
            TripPlan trip, CancellationToken ct = default)
        {
            _data[trip.Id] = trip;
            UpdateCount++;
            return Task.CompletedTask;
        }
    }
}
