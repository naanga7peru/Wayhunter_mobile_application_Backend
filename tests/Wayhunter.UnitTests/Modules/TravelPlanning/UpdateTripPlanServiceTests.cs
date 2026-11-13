
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Wayhunter.Application.Modules.TravelPlanning.Commands;
using Wayhunter.Application.Modules.TravelPlanning.Dtos;
using Wayhunter.Application.Modules.TravelPlanning.Interfaces;
using Wayhunter.Domain.Modules.TravelPlanning.Entities;

namespace Wayhunter.UnitTests.Modules.TravelPlanning;

public sealed class UpdateTripPlanServiceTests
{
    private static TripPlan CreateTrip()
    {
        return new TripPlan(
            "user-001",
            "Original Jaffna Trip",
            new DateOnly(2026, 11, 20),
            new DateOnly(2026, 11, 22),
            10000m,
            "Bus",
            2
        );
    }

    private static UpdateTripPlanDto ValidRequest()
    {
        return new UpdateTripPlanDto(
            "Updated Jaffna Trip",
            new DateOnly(2026, 11, 21),
            new DateOnly(2026, 11, 24),
            20000m,
            "Car",
            4
        );
    }

    [Fact]
    public async Task Update_ValidOwner_ShouldUpdateTrip()
    {
        var repository = new FakeTripPlanRepository();
        var trip = CreateTrip();

        await repository.AddAsync(trip);

        var service = new UpdateTripPlanService(repository);

        var result = await service.UpdateAsync(
            trip.Id,
            ValidRequest(),
            "user-001");

        Assert.NotNull(result);
        Assert.Equal("Updated Jaffna Trip", result.TripName);
        Assert.Equal(20000m, result.BudgetLkr);
        Assert.Equal("Car", result.VehicleType);
        Assert.Equal(4, result.Headcount);
        Assert.Equal(new DateOnly(2026, 11, 21),
            result.StartDate);
        Assert.Equal(1, repository.UpdateCalls);
    }

    [Fact]
    public async Task Update_DifferentOwner_ShouldReturnNull()
    {
        var repository = new FakeTripPlanRepository();
        var trip = CreateTrip();

        await repository.AddAsync(trip);

        var service = new UpdateTripPlanService(repository);

        var result = await service.UpdateAsync(
            trip.Id,
            ValidRequest(),
            "user-002");

        Assert.Null(result);
        Assert.Equal("Original Jaffna Trip", trip.TripName);
        Assert.Equal(0, repository.UpdateCalls);
    }

    [Fact]
    public async Task Update_UnknownTrip_ShouldReturnNull()
    {
        var repository = new FakeTripPlanRepository();

        var service = new UpdateTripPlanService(repository);

        var result = await service.UpdateAsync(
            Guid.NewGuid(),
            ValidRequest(),
            "user-001");

        Assert.Null(result);
        Assert.Equal(0, repository.UpdateCalls);
    }

    [Fact]
    public async Task Update_InvalidDates_ShouldThrowException()
    {
        var repository = new FakeTripPlanRepository();
        var trip = CreateTrip();

        await repository.AddAsync(trip);

        var service = new UpdateTripPlanService(repository);

        var request = ValidRequest() with
        {
            EndDate = new DateOnly(2026, 11, 19)
        };

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.UpdateAsync(
                trip.Id,
                request,
                "user-001"));

        Assert.Equal("Original Jaffna Trip", trip.TripName);
        Assert.Equal(0, repository.UpdateCalls);
    }

    [Fact]
    public async Task Update_MissingUserId_ShouldThrowException()
    {
        var repository = new FakeTripPlanRepository();

        var service = new UpdateTripPlanService(repository);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.UpdateAsync(
                Guid.NewGuid(),
                ValidRequest(),
                ""));

        Assert.Equal(0, repository.UpdateCalls);
    }

    [Fact]
    public async Task Update_EmptyTripId_ShouldThrowException()
    {
        var repository = new FakeTripPlanRepository();

        var service = new UpdateTripPlanService(repository);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.UpdateAsync(
                Guid.Empty,
                ValidRequest(),
                "user-001"));

        Assert.Equal(0, repository.UpdateCalls);
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
