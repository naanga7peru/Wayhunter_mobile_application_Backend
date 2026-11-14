
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Wayhunter.Application.Modules.TravelPlanning.Commands;
using Wayhunter.Application.Modules.TravelPlanning.Interfaces;
using Wayhunter.Domain.Modules.TravelPlanning.Entities;
using Wayhunter.Domain.Modules.TravelPlanning.Enums;

namespace Wayhunter.UnitTests.Modules.TravelPlanning;

public sealed class CancelTripPlanServiceTests
{
    private static TripPlan CreateTrip()
    {
        return new TripPlan(
            "user-001",
            "Jaffna Tour",
            new DateOnly(2026, 11, 20),
            new DateOnly(2026, 11, 22),
            15000m,
            "Car",
            3
        );
    }

    [Fact]
    public async Task Cancel_ValidOwner_ShouldSucceed()
    {
        var repository = new FakeTripPlanRepository();
        var trip = CreateTrip();

        await repository.AddAsync(trip);

        var service = new CancelTripPlanService(repository);

        var result = await service.CancelAsync(
            trip.Id, "user-001");

        Assert.True(result == true);
        Assert.Equal(
            TripDraftStatus.Cancelled,
            trip.DraftStatus);
        Assert.Equal(1, repository.UpdateCalls);
    }

    [Fact]
    public async Task Cancel_AlreadyCancelled_ShouldReturnFalse()
    {
        var repository = new FakeTripPlanRepository();
        var trip = CreateTrip();

        await repository.AddAsync(trip);

        var service = new CancelTripPlanService(repository);

        await service.CancelAsync(trip.Id, "user-001");

        var result = await service.CancelAsync(
            trip.Id, "user-001");

        Assert.Equal<bool?>(false, result);
        Assert.Equal(1, repository.UpdateCalls);
    }

    [Fact]
    public async Task Cancel_DifferentOwner_ShouldReturnNull()
    {
        var repository = new FakeTripPlanRepository();
        var trip = CreateTrip();

        await repository.AddAsync(trip);

        var service = new CancelTripPlanService(repository);

        var result = await service.CancelAsync(
            trip.Id, "user-002");

        Assert.Null(result);
        Assert.Equal(
            TripDraftStatus.Draft,
            trip.DraftStatus);
        Assert.Equal(0, repository.UpdateCalls);
    }

    [Fact]
    public async Task Cancel_UnknownTrip_ShouldReturnNull()
    {
        var repository = new FakeTripPlanRepository();
        var service = new CancelTripPlanService(repository);

        var result = await service.CancelAsync(
            Guid.NewGuid(), "user-001");

        Assert.Null(result);
        Assert.Equal(0, repository.UpdateCalls);
    }

    [Fact]
    public async Task Cancel_EmptyTripId_ShouldThrowException()
    {
        var repository = new FakeTripPlanRepository();
        var service = new CancelTripPlanService(repository);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.CancelAsync(
                Guid.Empty, "user-001"));
    }

    [Fact]
    public async Task Cancel_EmptyUserId_ShouldThrowException()
    {
        var repository = new FakeTripPlanRepository();
        var service = new CancelTripPlanService(repository);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.CancelAsync(
                Guid.NewGuid(), ""));
    }

    private sealed class FakeTripPlanRepository
        : ITripPlanRepository
    {
        private readonly Dictionary<Guid, TripPlan> _plans = new();

        public int UpdateCalls { get; private set; }

        public Task<TripPlan?> GetByIdAsync(
            Guid tripPlanId,
            CancellationToken cancellationToken = default)
        {
            _plans.TryGetValue(tripPlanId, out var trip);
            return Task.FromResult(trip);
        }

        public Task<IReadOnlyList<TripPlan>> GetByOwnerAsync(
            string ownerUserId,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<TripPlan> result = _plans.Values
                .Where(p => p.OwnerUserId == ownerUserId)
                .ToList();

            return Task.FromResult(result);
        }

        public Task AddAsync(
            TripPlan tripPlan,
            CancellationToken cancellationToken = default)
        {
            _plans.Add(tripPlan.Id, tripPlan);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(
            TripPlan tripPlan,
            CancellationToken cancellationToken = default)
        {
            _plans[tripPlan.Id] = tripPlan;
            UpdateCalls++;
            return Task.CompletedTask;
        }
    }
}
