using System;
using System.Collections.Generic;
using System.Text;
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
        if (tripPlanId == Guid.Empty)
            throw new ArgumentException(
                "Trip Plan ID is required.",
                nameof(tripPlanId));

        if (string.IsNullOrWhiteSpace(ownerUserId))
            throw new ArgumentException(
                "Authenticated User ID is required.",
                nameof(ownerUserId));

        var trip = await _repository.GetByIdAsync(
            tripPlanId,
            cancellationToken);

        // Users must not read another user's trip.
        if (trip is null || trip.OwnerUserId != ownerUserId)
            return null;

        var stops = trip.Stops
            .OrderBy(s => s.OrderIndex)
            .Select(s => new TripStopResponseDto(
                s.Id,
                s.OrderIndex,
                s.Name,
                s.PlaceId,
                s.Latitude,
                s.Longitude))
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

