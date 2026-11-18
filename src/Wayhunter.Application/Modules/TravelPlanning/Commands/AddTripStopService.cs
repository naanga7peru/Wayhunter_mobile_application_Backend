
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.TravelPlanning.Dtos;
using Wayhunter.Application.Modules.TravelPlanning.Exceptions;
using Wayhunter.Application.Modules.TravelPlanning.Interfaces;

namespace Wayhunter.Application.Modules.TravelPlanning.Commands;

public sealed class AddTripStopService
{
    private readonly ITripPlanRepository _repository;

    public AddTripStopService(ITripPlanRepository repository)
    {
        _repository = repository
            ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<TripPlanResponseDto?> AddAsync(
        Guid tripPlanId,
        AddTripStopDto request,
        string ownerUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (tripPlanId == Guid.Empty)
            throw new ArgumentException("Trip Plan ID is required.");

        if (string.IsNullOrWhiteSpace(ownerUserId))
            throw new ArgumentException("Authenticated User ID is required.");

        var trip = await _repository.GetByIdAsync(
            tripPlanId, cancellationToken);

        // Hide other users' trips.
        if (trip is null || trip.OwnerUserId != ownerUserId)
            return null;

        var currentVersion = trip.RowVersion;

        if (currentVersion.Length == 8)
        {
            // SQL persistence requires optimistic concurrency.
            if (request.ExpectedRowVersion is null ||
                request.ExpectedRowVersion.Length != 8)
            {
                throw new ArgumentException(
                    "An 8-byte RowVersion is required.");
            }

            if (!currentVersion.SequenceEqual(
                    request.ExpectedRowVersion))
            {
                throw new TripPlanConcurrencyException(tripPlanId);
            }
        }
        else if (currentVersion.Length == 0)
        {
            // Temporary InMemory demo repository has no SQL
            // concurrency token. Do not treat it as SQL-safe.
            if (request.ExpectedRowVersion is { Length: > 0 })
            {
                throw new ArgumentException(
                    "RowVersion is not available in demo storage.");
            }
        }
        else
        {
            throw new InvalidOperationException(
                "Invalid stored RowVersion.");
        }

        trip.AddStop(
            request.Name,
            request.PlaceId,
            request.Latitude,
            request.Longitude);

        if (currentVersion.Length == 8)
        {
            await _repository.UpdateWithConcurrencyAsync(
                trip,
                request.ExpectedRowVersion!,
                cancellationToken);
        }
        else
        {
            await _repository.UpdateAsync(
                trip, cancellationToken);
        }

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
