
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.TravelPlanning.Dtos;
using Wayhunter.Application.Modules.TravelPlanning.Exceptions;
using Wayhunter.Application.Modules.TravelPlanning.Interfaces;
using Wayhunter.Domain.Modules.TravelPlanning.Enums;

namespace Wayhunter.Application.Modules.TravelPlanning.Commands;

public sealed class RemoveTripStopService
{
    private readonly ITripPlanRepository _repository;

    public RemoveTripStopService(ITripPlanRepository repository)
    {
        _repository = repository
            ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<TripPlanResponseDto?> RemoveAsync(
        Guid tripPlanId,
        Guid stopId,
        RemoveTripStopDto request,
        string ownerUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (tripPlanId == Guid.Empty || stopId == Guid.Empty)
            throw new ArgumentException(
                "Valid Trip Plan ID and Stop ID are required.");

        if (string.IsNullOrWhiteSpace(ownerUserId))
            throw new ArgumentException(
                "Authenticated User ID is required.");

        var trip = await _repository.GetByIdAsync(
            tripPlanId, cancellationToken);

        // Never expose another user's trip.
        if (trip is null || trip.OwnerUserId != ownerUserId)
            return null;

        // Stop must belong to this trip.
        if (!trip.Stops.Any(stop => stop.Id == stopId))
            return null;

        if (trip.DraftStatus == TripDraftStatus.Cancelled)
            throw new InvalidOperationException(
                "Cancelled trip plans cannot be edited.");

        var rowVersion = trip.RowVersion;

        if (rowVersion.Length == 8)
        {
            if (request.ExpectedRowVersion is null ||
                request.ExpectedRowVersion.Length != 8)
            {
                throw new ArgumentException(
                    "An 8-byte RowVersion is required.");
            }

            if (!rowVersion.SequenceEqual(
                    request.ExpectedRowVersion))
            {
                throw new TripPlanConcurrencyException(tripPlanId);
            }
        }
        else if (rowVersion.Length == 0)
        {
            if (request.ExpectedRowVersion is { Length: > 0 })
                throw new ArgumentException(
                    "RowVersion is unavailable in demo storage.");
        }
        else
        {
            throw new InvalidOperationException(
                "Invalid stored RowVersion.");
        }

        var updated = await _repository
            .RemoveStopWithConcurrencyAsync(
                trip,
                stopId,
                request.ExpectedRowVersion,
                cancellationToken);

        var stops = updated.Stops
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
            updated.Id,
            updated.TripName,
            updated.StartDate,
            updated.EndDate,
            updated.BudgetLkr,
            updated.VehicleType,
            updated.Headcount,
            stops)
        {
            RowVersion = updated.RowVersion.ToArray()
        };
    }
}
