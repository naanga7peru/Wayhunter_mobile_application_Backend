using System;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Domain.Modules.TravelPlanning.Entities;

namespace Wayhunter.Application.Modules.TravelPlanning.Interfaces;

// The store must atomically re-check viewing AND reuse permission,
// authoritative M3 finish evidence and current M3 membership.
// Returning null avoids disclosing whether a private source exists.
public interface ICopySuggestedTripPlanStore
{
    Task<TripPlan?> CopyEligibleAsync(
        Guid sourceTripPlanId,
        string authenticatedUserId,
        CancellationToken cancellationToken = default);
}
