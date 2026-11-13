
using Xunit;
using Wayhunter.Application.Modules.TravelPlanning.Commands;
using Wayhunter.Application.Modules.TravelPlanning.Dtos;
using Wayhunter.Application.Modules.TravelPlanning.Interfaces;
using Wayhunter.Domain.Modules.TravelPlanning.Entities;

namespace Wayhunter.UnitTests.Modules.TravelPlanning;

public sealed class CreateTripPlanServiceTests
{
    private static CreateTripPlanDto ValidRequest()
    {
        return new CreateTripPlanDto(
            "Jaffna Tour",
            new DateOnly(2026, 11, 20),
            new DateOnly(2026, 11, 22),
            15000m,
            "Car",
            3
        );
    }

    [Fact]
    public async Task CreateAsync_ValidRequest_ShouldCreateTrip()
    {
        var repository = new FakeTripPlanRepository();
        var service = new CreateTripPlanService(repository);

        var result = await service.CreateAsync(
            ValidRequest(), "user-001");

        Assert.NotEqual(Guid.Empty, result.TripPlanId);
        Assert.Equal("Jaffna Tour", result.TripName);
        Assert.Equal(15000m, result.BudgetLkr);
        Assert.Empty(result.Stops);
    }

    [Fact]
    public async Task CreateAsync_ValidRequest_ShouldSaveToRepository()
    {
        var repository = new FakeTripPlanRepository();
        var service = new CreateTripPlanService(repository);

        var result = await service.CreateAsync(
            ValidRequest(), "user-001");

        var saved = await repository.GetByIdAsync(
            result.TripPlanId);

        Assert.NotNull(saved);
        Assert.Equal("user-001", saved.OwnerUserId);
        Assert.Equal("Jaffna Tour", saved.TripName);
    }

    [Fact]
    public async Task CreateAsync_InvalidDates_ShouldFail()
    {
        var repository = new FakeTripPlanRepository();
        var service = new CreateTripPlanService(repository);

        var request = ValidRequest() with
        {
            EndDate = new DateOnly(2026, 11, 19)
        };

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.CreateAsync(request, "user-001"));

        var saved = await repository.GetByOwnerAsync("user-001");
        Assert.Empty(saved);
    }

    [Fact]
    public async Task CreateAsync_MissingUserId_ShouldFail()
    {
        var repository = new FakeTripPlanRepository();
        var service = new CreateTripPlanService(repository);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.CreateAsync(
                ValidRequest(), ""));

        var saved = await repository.GetByOwnerAsync("");
        Assert.Empty(saved);
    }

    private sealed class FakeTripPlanRepository
        : ITripPlanRepository
    {
        private readonly Dictionary<Guid, TripPlan> _plans = new();

        public Task<TripPlan?> GetByIdAsync(
            Guid tripPlanId,
            CancellationToken cancellationToken = default)
        {
            _plans.TryGetValue(tripPlanId, out var plan);
            return Task.FromResult(plan);
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
