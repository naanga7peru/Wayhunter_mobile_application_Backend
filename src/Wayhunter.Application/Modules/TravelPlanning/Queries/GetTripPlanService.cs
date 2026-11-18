
using System;
using System.Collections.Generic;
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

    // Get a single trip plan by ID.
    public async Task<TripPlanResponseDto?> GetByIdAsync(
        Guid tripPlanId,
        string ownerUserId,
        CancellationToken cancellationToken = default)
    {
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

        var trip = await _repository.GetByIdAsync(
            tripPlanId,
            cancellationToken);

        if (trip is null || trip.OwnerUserId != ownerUserId)
        {
            return null;
        }

        return MapToResponse(trip);
    }

    // Get all trip plans belonging to the authenticated user.
    public async Task<IReadOnlyList<TripPlanResponseDto>> GetByOwnerAsync(
        string ownerUserId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ownerUserId))
        {
            throw new ArgumentException(
                "Authenticated User ID is required.",
                nameof(ownerUserId));
        }

        var trips = await _repository.GetByOwnerAsync(
            ownerUserId,
            cancellationToken);

        return trips
            .Where(trip => trip.OwnerUserId == ownerUserId)
            .OrderByDescending(trip => trip.StartDate)
            .ThenBy(trip => trip.Id)
            .Select(MapToResponse)
            .ToList();
    }

    // Shared response mapping for single and list queries.
    private static TripPlanResponseDto MapToResponse(
        Wayhunter.Domain.Modules.TravelPlanning.Entities.TripPlan trip)
    {
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
            stops)
        {
            RowVersion = trip.RowVersion.ToArray()
        };
    }
}
