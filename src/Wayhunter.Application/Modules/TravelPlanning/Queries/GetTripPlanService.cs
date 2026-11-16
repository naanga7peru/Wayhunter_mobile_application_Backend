
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.TravelPlanning.Dtos;
using Wayhunter.Application.Modules.TravelPlanning.Interfaces;

namespace Wayhunter.Application.Modules.TravelPlanning.Queries;

public sealed class GetTripPlanService
{
    private readonly ITripPlanRepository _repository;

    public GetTripPlanService(ITripPlanRepository repository)
    {
        _repository = repository
            ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<TripPlanResponseDto?> GetByIdAsync(
        Guid tripPlanId,
        string ownerUserId,
        CancellationToken cancellationToken = default)
    {
        // Step 1: Validate request.
        if (tripPlanId == Guid.Empty)
        {
            throw new ArgumentException(
                "Trip Plan ID is required.",
                nameof(tripPlanId));
        }

        if (string.IsNullOrWhiteSpace(ownerUserId))
        {
            throw new ArgumentException(
                "Authenticated User ID is required.",
                nameof(ownerUserId));
        }

        // Step 2: Retrieve existing trip.
        var trip = await _repository.GetByIdAsync(
            tripPlanId,
            cancellationToken);

        // Step 3: Verify ownership.
        if (trip is null || trip.OwnerUserId != ownerUserId)
        {
            return null;
        }

        // Step 4: Map ordered stops.
        var stops = trip.Stops
            .OrderBy(stop => stop.OrderIndex)
            .Select(stop => new TripStopResponseDto(
                stop.Id,
                stop.OrderIndex,
                stop.Name,
                stop.PlaceId,
                stop.Latitude,
                stop.Longitude))
            .ToList();

        // Step 5: Return trip with latest RowVersion.
        return new TripPlanResponseDto(
            trip.Id,
            trip.TripName,
            trip.StartDate,
            trip.EndDate,
            trip.BudgetLkr,
            trip.VehicleType,
            trip.Headcount,
            stops)
        {
            RowVersion = trip.RowVersion.ToArray()
        };
    }
}
