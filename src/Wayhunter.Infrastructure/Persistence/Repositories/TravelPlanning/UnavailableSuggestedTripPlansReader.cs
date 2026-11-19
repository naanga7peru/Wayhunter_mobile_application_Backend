using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.TravelPlanning.Dtos;
using Wayhunter.Application.Modules.TravelPlanning.Interfaces;

namespace Wayhunter.Infrastructure.Persistence.Repositories.TravelPlanning;

// Demo mode has no authoritative completed + shared trip evidence.
// Fail closed: never suggest unsafely from in-memory draft trips.
public sealed class UnavailableSuggestedTripPlansReader
    : ISuggestedTripPlansReader
{
    public Task<IReadOnlyList<SuggestedTripPlanDto>> GetEligibleAsync(
        string authenticatedUserId,
        int maxResults,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(authenticatedUserId);

        return Task.FromResult<IReadOnlyList<SuggestedTripPlanDto>>(
            Array.Empty<SuggestedTripPlanDto>());
    }
}
