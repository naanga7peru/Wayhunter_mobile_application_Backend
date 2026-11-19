using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.TravelPlanning.Dtos;
using Wayhunter.Application.Modules.TravelPlanning.Interfaces;

namespace Wayhunter.Application.Modules.TravelPlanning.Commands;

public sealed class CopySuggestedTripPlanService
{
    private readonly ICopySuggestedTripPlanStore _store;

    public CopySuggestedTripPlanService(ICopySuggestedTripPlanStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<CopiedSuggestedTripPlanDto?> CopyAsync(
        Guid sourceTripPlanId,
        string authenticatedUserId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (sourceTripPlanId == Guid.Empty)
            throw new ArgumentException("A source trip plan ID is required.", nameof(sourceTripPlanId));
        if (string.IsNullOrWhiteSpace(authenticatedUserId))
            throw new ArgumentException("Authenticated user ID is required.", nameof(authenticatedUserId));

        // The source cannot be fetched through GetByIdAsync, which is owner-only.
        // The store performs authorization + copy as one database transaction.
        var copied = await _store.CopyEligibleAsync(
            sourceTripPlanId, authenticatedUserId, cancellationToken);
        if (copied is null) return null;

        // Defense in depth: refuse misconfigured/fake stores returning wrong owner.
        if (copied.Id == sourceTripPlanId ||
            copied.OwnerUserId != authenticatedUserId ||
            copied.SourceTripPlanId != sourceTripPlanId ||
            string.IsNullOrWhiteSpace(copied.SourceOwnerUserId))
            throw new InvalidOperationException("Copied trip store returned invalid provenance.");

        var stops = copied.Stops
            .OrderBy(stop => stop.OrderIndex)
            .Select(stop => new TripStopResponseDto(
                stop.Id, stop.OrderIndex, stop.Name,
                stop.PlaceId, stop.Latitude, stop.Longitude))
            .ToArray();

        return new CopiedSuggestedTripPlanDto(
            copied.Id, sourceTripPlanId, copied.TripName,
            copied.StartDate, copied.EndDate, copied.BudgetLkr,
            copied.VehicleType, copied.Headcount, stops)
        {
            RowVersion = copied.RowVersion.ToArray()
        };
    }
}
