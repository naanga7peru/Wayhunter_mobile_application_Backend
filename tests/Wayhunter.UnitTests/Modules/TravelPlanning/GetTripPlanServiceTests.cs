
using Xunit;
using Wayhunter.Application.Modules.TravelPlanning.Interfaces;
using Wayhunter.Application.Modules.TravelPlanning.Queries;
using Wayhunter.Domain.Modules.TravelPlanning.Entities;

namespace Wayhunter.UnitTests.Modules.TravelPlanning;

public sealed class GetTripPlanServiceTests
{
    private static TripPlan CreateTrip(string ownerUserId = "user-001")
    {
        return new TripPlan(
            ownerUserId,
            "Jaffna Tour",
            new DateOnly(2026, 11, 20),
            new DateOnly(2026, 11, 22),
            15000m,
            "Car",
            3
        );
    }

    [Fact]
    public async Task GetById_ValidOwner_ShouldReturnTrip()
    {
        var repository = new FakeTripPlanRepository();
        var trip = CreateTrip();

        await repository.AddAsync(trip);

        var service = new GetTripPlanService(repository);

        var result = await service.GetByIdAsync(
            trip.Id, "user-001");

        Assert.NotNull(result);
        Assert.Equal(trip.Id, result.TripPlanId);
        Assert.Equal("Jaffna Tour", result.TripName);
    }

    [Fact]
    public async Task GetById_DifferentOwner_ShouldReturnNull()
    {
        var repository = new FakeTripPlanRepository();
        var trip = CreateTrip("user-001");

        await repository.AddAsync(trip);

        var service = new GetTripPlanService(repository);

        var result = await service.GetByIdAsync(
            trip.Id, "user-002");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetById_UnknownTrip_ShouldReturnNull()
    {
        var repository = new FakeTripPlanRepository();
        var service = new GetTripPlanService(repository);

        var result = await service.GetByIdAsync(
            Guid.NewGuid(), "user-001");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetById_ShouldReturnOrderedStops()
    {
        var repository = new FakeTripPlanRepository();
        var trip = CreateTrip();

        trip.AddStop("Jaffna Fort");
        trip.AddStop("Nallur Temple");
        trip.AddStop("Point Pedro");

        await repository.AddAsync(trip);

        var service = new GetTripPlanService(repository);

        var result = await service.GetByIdAsync(
            trip.Id, "user-001");

        Assert.NotNull(result);
        Assert.Equal(3, result.Stops.Count);

        Assert.Equal("Jaffna Fort", result.Stops[0].Name);
        Assert.Equal("Nallur Temple", result.Stops[1].Name);
        Assert.Equal("Point Pedro", result.Stops[2].Name);

        Assert.Equal(0, result.Stops[0].OrderIndex);
        Assert.Equal(1, result.Stops[1].OrderIndex);
        Assert.Equal(2, result.Stops[2].OrderIndex);
    }

    [Fact]
    public async Task GetById_EmptyTripId_ShouldThrowException()
    {
        var repository = new FakeTripPlanRepository();
        var service = new GetTripPlanService(repository);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.GetByIdAsync(
                Guid.Empty, "user-001"));
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
