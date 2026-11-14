
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.TravelPlanning.Dtos;
using Wayhunter.Application.Modules.TravelPlanning.Interfaces;

namespace Wayhunter.Application.Modules.TravelPlanning.Commands;

public sealed class UpdateTripPlanService
{
    private readonly ITripPlanRepository _repository;

    public UpdateTripPlanService(ITripPlanRepository repository)
    {
        _repository = repository
            ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<TripPlanResponseDto?> UpdateAsync(
        Guid tripPlanId,
        UpdateTripPlanDto request,
        string ownerUserId,
        CancellationToken cancellationToken = default)
    {
        // Step 1: Validate request
        ArgumentNullException.ThrowIfNull(request);

        if (tripPlanId == Guid.Empty)
            throw new ArgumentException(
                "Trip Plan ID is required.",
                nameof(tripPlanId));

        if (string.IsNullOrWhiteSpace(ownerUserId))
            throw new ArgumentException(
                "Authenticated User ID is required.",
                nameof(ownerUserId));

        // Step 2: Find existing trip
        var trip = await _repository.GetByIdAsync(
            tripPlanId,
            cancellationToken);

        // Step 3: Verify trip ownership
        if (trip is null || trip.OwnerUserId != ownerUserId)
            return null;

        // Step 4: Update trip details
        // Domain method validates the new values.
        trip.UpdateDetails(
            request.TripName,
            request.StartDate,
            request.EndDate,
            request.BudgetLkr,
            request.VehicleType,
            request.Headcount);

        // Step 5: Save updated trip
        await _repository.UpdateAsync(
            trip,
            cancellationToken);

        // Step 6: Map entity to response DTO
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

        return new TripPlanResponseDto(
            trip.Id,
            trip.TripName,
            trip.StartDate,
            trip.EndDate,
            trip.BudgetLkr,
            trip.VehicleType,
            trip.Headcount,
            stops);
    }
}
