using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.TravelPlanning.Dtos;

namespace Wayhunter.Application.Modules.TravelPlanning.Interfaces;

// Every implementation must enforce authorization + completion at the source.
// Do not use ITripPlanRepository.GetByOwnerAsync to discover others' plans.
public interface ISuggestedTripPlansReader
{
    Task<IReadOnlyList<SuggestedTripPlanDto>> GetEligibleAsync(
        string authenticatedUserId,
        int maxResults,
        CancellationToken cancellationToken = default);
}
