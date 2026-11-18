
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.TravelPlanning.Interfaces;
using Wayhunter.Application.Modules.TravelPlanning.Queries;
using Wayhunter.Domain.Modules.TravelPlanning.Entities;
using Xunit;

namespace Wayhunter.UnitTests.Modules.TravelPlanning;

public sealed class GetMyTripPlansServiceTests
{
    private static TripPlan CreateTrip(
        string ownerUserId,
        string name,
        int startDay)
    {
        return new TripPlan(
            ownerUserId,
            name,
            new DateOnly(2026, 11, startDay),
            new DateOnly(2026, 11, startDay + 1),
            15000m,
            "Car",
            3);
    }

    [Fact]
    public async Task EmptyRepository_ReturnsEmptyList()
    {
        var repository = new FakeTripPlanRepository();
        var service = new GetTripPlanService(repository);

        var result = await service.GetByOwnerAsync("user-001");

        Assert.Empty(result);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public async Task InvalidOwner_ThrowsArgumentException(
        string ownerUserId)
    {
        var repository = new FakeTripPlanRepository();
        var service = new GetTripPlanService(repository);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.GetByOwnerAsync(ownerUserId));
    }

    [Fact]
    public async Task GetByOwner_ReturnsOnlyAuthenticatedUsersTrips()
    {
        var repository = new FakeTripPlanRepository();

        var ownTrip1 = CreateTrip(
            "user-001", "Jaffna Tour", 20);

        var otherTrip = CreateTrip(
            "user-002", "Colombo Tour", 21);

        var ownTrip2 = CreateTrip(
            "user-001", "Kandy Tour", 22);

        await repository.AddAsync(ownTrip1);
        await repository.AddAsync(otherTrip);
        await repository.AddAsync(ownTrip2);

        var service = new GetTripPlanService(repository);

        var result = await service.GetByOwnerAsync("user-001");

        Assert.Equal(2, result.Count);
        Assert.Contains(result,
            trip => trip.TripPlanId == ownTrip1.Id);
        Assert.Contains(result,
            trip => trip.TripPlanId == ownTrip2.Id);
        Assert.DoesNotContain(result,
            trip => trip.TripPlanId == otherTrip.Id);
    }

    [Fact]
    public async Task GetByOwner_ReturnsLatestStartDateFirst()
    {
        var repository = new FakeTripPlanRepository();

        var oldest = CreateTrip(
            "user-001", "Old Trip", 19);

        var newest = CreateTrip(
            "user-001", "New Trip", 25);

        var middle = CreateTrip(
            "user-001", "Middle Trip", 22);

        await repository.AddAsync(oldest);
        await repository.AddAsync(newest);
        await repository.AddAsync(middle);

        var service = new GetTripPlanService(repository);

        var result = await service.GetByOwnerAsync("user-001");

        Assert.Equal(3, result.Count);
        Assert.Equal(newest.Id, result[0].TripPlanId);
        Assert.Equal(middle.Id, result[1].TripPlanId);
        Assert.Equal(oldest.Id, result[2].TripPlanId);
    }

    [Fact]
    public async Task GetByOwner_ReturnsStopsInCorrectOrder()
    {
        var repository = new FakeTripPlanRepository();

        var trip = CreateTrip(
            "user-001", "Jaffna Tour", 20);

        trip.AddStop("Jaffna Fort");
        trip.AddStop("Nallur Temple");
        trip.AddStop("Point Pedro");

        await repository.AddAsync(trip);

        var service = new GetTripPlanService(repository);

        var result = await service.GetByOwnerAsync("user-001");

        var stops = Assert.Single(result).Stops;

        Assert.Equal(3, stops.Count);
        Assert.Equal("Jaffna Fort", stops[0].Name);
        Assert.Equal("Nallur Temple", stops[1].Name);
        Assert.Equal("Point Pedro", stops[2].Name);

        Assert.Equal(0, stops[0].OrderIndex);
        Assert.Equal(1, stops[1].OrderIndex);
        Assert.Equal(2, stops[2].OrderIndex);
    }

    [Fact]
    public async Task GetByOwner_ReturnsRowVersion()
    {
        var repository = new FakeTripPlanRepository();

        var trip = CreateTrip(
            "user-001", "Jaffna Tour", 20);

        await repository.AddAsync(trip);

        var service = new GetTripPlanService(repository);

        var result = await service.GetByOwnerAsync("user-001");

        var response = Assert.Single(result);

        Assert.Equal(
            trip.RowVersion,
            response.RowVersion);
    }

    private sealed class FakeTripPlanRepository
        : ITripPlanRepository
    {
        private readonly Dictionary<Guid, TripPlan> _plans = new();

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
            return Task.CompletedTask;
        }
    }
}
