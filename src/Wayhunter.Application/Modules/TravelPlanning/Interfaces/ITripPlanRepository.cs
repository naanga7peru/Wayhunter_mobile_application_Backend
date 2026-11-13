
using Wayhunter.Domain.Modules.TravelPlanning.Entities;

namespace Wayhunter.Application.Modules.TravelPlanning.Interfaces;

public interface ITripPlanRepository
{
    Task<TripPlan?> GetByIdAsync(
        Guid tripPlanId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TripPlan>> GetByOwnerAsync(
        string ownerUserId,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        TripPlan tripPlan,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        TripPlan tripPlan,
        CancellationToken cancellationToken = default);
}
