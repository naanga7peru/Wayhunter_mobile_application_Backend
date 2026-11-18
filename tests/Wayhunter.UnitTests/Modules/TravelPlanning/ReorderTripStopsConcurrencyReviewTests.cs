
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

public sealed class ReorderTripStopsConcurrencyReviewTests
{
    private static readonly byte[] OriginalVersion =
        { 1, 2, 3, 4, 5, 6, 7, 8 };

    private static readonly byte[] NewerVersion =
        { 2, 3, 4, 5, 6, 7, 8, 9 };

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

        typeof(TripPlan)
            .GetProperty(
                nameof(TripPlan.RowVersion),
                BindingFlags.Instance | BindingFlags.Public)!
            .SetValue(trip, OriginalVersion.ToArray());

        return trip;
    }

    // ==========================================
    // TEST 01
    // Unchanged order must reach SQL concurrency guard.
    // ==========================================
    [Fact]
    public async Task UnchangedOrder_WithSqlRowVersion_UsesGuard()
    {
        var trip = CreateTrip();
        var repo = new ConcurrencyRepository(trip);
        var service = new ReorderTripStopsService(repo);

        var currentOrder = trip.Stops
            .OrderBy(stop => stop.OrderIndex)
            .Select(stop => stop.Id)
            .ToArray();

        var result = await service.ReorderAsync(
            trip.Id,
            new ReorderTripStopsDto(
                currentOrder,
                OriginalVersion.ToArray()),
            "user-001");

        Assert.NotNull(result);

        // Critical reviewer requirement:
        // No-op must not bypass repository concurrency.
        Assert.Equal(1, repo.GuardCallCount);

        Assert.Equal(
            currentOrder,

           result.Stops.Select(stop => stop.StopId));

    }

    // ==========================================
    // TEST 02
    // RowVersion changes after initial read.
    // No-op request must throw concurrency conflict.
    // ==========================================
    [Fact]
    public async Task UnchangedOrder_ConcurrentUpdate_ThrowsConflict()
    {
        var trip = CreateTrip();

        var repo = new ConcurrencyRepository(trip)
        {
            SimulateConcurrentUpdateAfterRead = true
        };

        var service = new ReorderTripStopsService(repo);

        var currentOrder = trip.Stops
            .OrderBy(stop => stop.OrderIndex)
            .Select(stop => stop.Id)
            .ToArray();

        await Assert.ThrowsAsync<TripPlanConcurrencyException>(
            () => service.ReorderAsync(
                trip.Id,
                new ReorderTripStopsDto(
                    currentOrder,
                    OriginalVersion.ToArray()),
                "user-001"));

        Assert.Equal(1, repo.GuardCallCount);
    }

    // ==========================================
    // TEST 03
    // Changed order still uses concurrency guard.
    // ==========================================
    [Fact]
    public async Task ChangedOrder_UsesGuardAndReorders()
    {
        var trip = CreateTrip();
        var repo = new ConcurrencyRepository(trip);
        var service = new ReorderTripStopsService(repo);

        var newOrder = new[]
        {
            trip.Stops[2].Id,
            trip.Stops[0].Id,
            trip.Stops[1].Id
        };

        var result = await service.ReorderAsync(
            trip.Id,
            new ReorderTripStopsDto(
                newOrder,
                OriginalVersion.ToArray()),
            "user-001");

        Assert.NotNull(result);
        Assert.Equal(1, repo.GuardCallCount);

        Assert.Equal(
            newOrder,

             result.Stops.Select(stop => stop.StopId));

        Assert.Equal(
            new[] { 0, 1, 2 },
            result.Stops.Select(stop => stop.OrderIndex));
    }

    // ==========================================
    // TEST-ONLY REPOSITORY
    // Simulates SQL RowVersion changing between
    // initial read and repository guard.
    // ==========================================
    private sealed class ConcurrencyRepository
        : ITripPlanRepository
    {
        private readonly TripPlan _trip;
        private byte[] _databaseVersion;

        public int GuardCallCount { get; private set; }

        public bool SimulateConcurrentUpdateAfterRead
        {
            get;
            set;
        }

        public ConcurrencyRepository(TripPlan trip)
        {
            _trip = trip;
            _databaseVersion = OriginalVersion.ToArray();
        }

        public Task<TripPlan?> GetByIdAsync(
            Guid id,
            CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            if (id != _trip.Id)
            {
                return Task.FromResult<TripPlan?>(null);
            }

            // Simulate another transaction changing
            // the database RowVersion after the read.
            if (SimulateConcurrentUpdateAfterRead)
            {
                _databaseVersion = NewerVersion.ToArray();
            }

            return Task.FromResult<TripPlan?>(_trip);
        }

        public Task<TripPlan> ReorderStopsWithConcurrencyAsync(
            TripPlan tripPlan,
            IReadOnlyList<Guid> orderedStopIds,
            byte[]? expectedRowVersion,
            CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            GuardCallCount++;

            // Simulated SQL atomic RowVersion guard.
            if (expectedRowVersion is null ||
                !_databaseVersion.SequenceEqual(
                    expectedRowVersion))
            {
                throw new TripPlanConcurrencyException(
                    tripPlan.Id);
            }

            tripPlan.ReorderStops(orderedStopIds);

            return Task.FromResult(tripPlan);
        }

        public Task<IReadOnlyList<TripPlan>> GetByOwnerAsync(
            string owner,
            CancellationToken ct = default)
        {
            return Task.FromResult<IReadOnlyList<TripPlan>>(
                Array.Empty<TripPlan>());
        }

        public Task AddAsync(
            TripPlan trip,
            CancellationToken ct = default)
        {
            return Task.CompletedTask;
        }

        public Task UpdateAsync(
            TripPlan trip,
            CancellationToken ct = default)
        {
            return Task.CompletedTask;
        }
    }
}
