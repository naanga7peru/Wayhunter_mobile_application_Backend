
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.TravelPlanning.Dtos;
using Wayhunter.Application.Modules.TravelPlanning.Exceptions;
using Wayhunter.Application.Modules.TravelPlanning.Interfaces;

namespace Wayhunter.Application.Modules.TravelPlanning.Commands;

public sealed class UpdateTripPlanService
{
    private const int SqlServerRowVersionLength = 8;

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
        ArgumentNullException.ThrowIfNull(request);

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

        // Step 1: Validate the client's expected RowVersion.
        var expectedRowVersion = request.ExpectedRowVersion;

        if (expectedRowVersion is null ||
            expectedRowVersion.Length != SqlServerRowVersionLength)
        {
            throw new ArgumentException(
                "A valid 8-byte RowVersion is required for trip updates.",
                nameof(request));
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

        // Step 4: Reject stale requests BEFORE mutating the entity.
        if (trip.RowVersion is null ||
            !trip.RowVersion.SequenceEqual(expectedRowVersion))
        {
            throw new TripPlanConcurrencyException(tripPlanId);
        }

        // Step 5: Update domain entity.
        trip.UpdateDetails(
            request.TripName,
            request.StartDate,
            request.EndDate,
            request.BudgetLkr,
            request.VehicleType,
            request.Headcount);

        // Step 6: Persist with atomic SQL concurrency protection.
        // EF repository must use the expected RowVersion
        // as the original concurrency token.
        await _repository.UpdateWithConcurrencyAsync(
            trip,
            expectedRowVersion.ToArray(),
            cancellationToken);

        // Step 7: Map ordered stops.
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

        // Step 8: Return updated trip.
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
