
using System.Collections.Concurrent;
using Wayhunter.Application.Modules.TravelPlanning.Interfaces;
using Wayhunter.Domain.Modules.TravelPlanning.Entities;

namespace Wayhunter.Infrastructure.Persistence.Repositories.TravelPlanning;

public sealed class InMemoryTripPlanRepository : ITripPlanRepository
{
    private readonly ConcurrentDictionary<Guid, TripPlan> _tripPlans = new();

    public Task<TripPlan?> GetByIdAsync(
        Guid tripPlanId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _tripPlans.TryGetValue(tripPlanId, out var tripPlan);

        return Task.FromResult(tripPlan);
    }

    public Task<IReadOnlyList<TripPlan>> GetByOwnerAsync(
        string ownerUserId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var plans = _tripPlans.Values
            .Where(p => p.OwnerUserId == ownerUserId)
            .ToList();

        return Task.FromResult<IReadOnlyList<TripPlan>>(plans);
    }

    public Task AddAsync(
        TripPlan tripPlan,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(tripPlan);

        if (!_tripPlans.TryAdd(tripPlan.Id, tripPlan))
        {
            throw new InvalidOperationException(
                "Trip plan already exists.");
        }

        return Task.CompletedTask;
    }

    public Task UpdateAsync(
        TripPlan tripPlan,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(tripPlan);

        if (!_tripPlans.ContainsKey(tripPlan.Id))
        {
            throw new KeyNotFoundException(
                "Trip plan not found.");
        }

        _tripPlans[tripPlan.Id] = tripPlan;

        return Task.CompletedTask;
    }
}
