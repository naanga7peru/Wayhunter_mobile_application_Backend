using System;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.TravelPlanning.Interfaces;
using Wayhunter.Domain.Modules.TravelPlanning.Entities;

namespace Wayhunter.Infrastructure.Persistence.Repositories.TravelPlanning;

// Default fail-closed store: no verified reuse permission policy is configured.
public sealed class UnavailableCopySuggestedTripPlanStore : ICopySuggestedTripPlanStore
{
    public Task<TripPlan?> CopyEligibleAsync(
        Guid sourceTripPlanId,
        string authenticatedUserId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<TripPlan?>(null);
    }
}
