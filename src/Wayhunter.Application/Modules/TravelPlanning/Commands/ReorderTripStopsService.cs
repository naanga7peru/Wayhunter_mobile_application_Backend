
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.TravelPlanning.Dtos;
using Wayhunter.Application.Modules.TravelPlanning.Exceptions;
using Wayhunter.Application.Modules.TravelPlanning.Interfaces;
using Wayhunter.Domain.Modules.TravelPlanning.Enums;

namespace Wayhunter.Application.Modules.TravelPlanning.Commands;

public sealed class ReorderTripStopsService
{
    private readonly ITripPlanRepository _repository;

    public ReorderTripStopsService(
        ITripPlanRepository repository)
    {
        _repository = repository
            ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<TripPlanResponseDto?> ReorderAsync(
        Guid tripPlanId,
        ReorderTripStopsDto request,
        string ownerUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (tripPlanId == Guid.Empty)
            throw new ArgumentException(
                "Valid Trip Plan ID is required.");

        if (string.IsNullOrWhiteSpace(ownerUserId))
            throw new ArgumentException(
                "Authenticated User ID is required.");

        if (request.StopIds is null)
            throw new ArgumentException(
                "Stop IDs are required.");

        // STEP 1 - Load trip.
        var trip = await _repository.GetByIdAsync(
            tripPlanId,
            cancellationToken);

        // STEP 2 - Validate ownership.
        if (trip is null || trip.OwnerUserId != ownerUserId)
            return null;

        // STEP 3 - Reject cancelled trip.
        if (trip.DraftStatus == TripDraftStatus.Cancelled)
            throw new InvalidOperationException(
                "Cancelled trip plans cannot be edited.");

        // STEP 4 - Validate RowVersion.
        var currentVersion = trip.RowVersion;

        if (currentVersion.Length == 8)
        {
            if (request.ExpectedRowVersion is null ||
                request.ExpectedRowVersion.Length != 8)
            {
                throw new ArgumentException(
                    "An 8-byte RowVersion is required.");
            }

            if (!currentVersion.SequenceEqual(
                    request.ExpectedRowVersion))
            {
                throw new TripPlanConcurrencyException(
                    tripPlanId);
            }
        }
        else if (currentVersion.Length == 0)
        {
            // In-memory demo storage.
            if (request.ExpectedRowVersion is { Length: > 0 })
            {
                throw new ArgumentException(
                    "RowVersion is unavailable in demo storage.");
            }
        }
        else
        {
            throw new InvalidOperationException(
                "Invalid stored RowVersion.");
        }

        // STEP 5 - Validate submitted stop IDs.
        var existingIds = trip.Stops
            .Select(stop => stop.Id)
            .ToHashSet();

        if (request.StopIds.Count != existingIds.Count ||
            request.StopIds.Distinct().Count() !=
                request.StopIds.Count ||
            request.StopIds.Any(
                stopId => !existingIds.Contains(stopId)))
        {
            throw new ArgumentException(
                "Every existing stop must be included exactly once.");
        }

        // STEP 6 - Detect current ordering.
        var currentOrder = trip.Stops
            .OrderBy(stop => stop.OrderIndex)
            .Select(stop => stop.Id);

        var changed = !currentOrder.SequenceEqual(
            request.StopIds);

        // STEP 7 - SQL-backed trips must always
        // enter the repository's atomic concurrency
        // verification path, even if order is unchanged.
        //
        // Otherwise, concurrent DB changes after the
        // initial read could be missed.
        //
        // Demo in-memory storage can skip no-op writes.
        var requiresSqlConcurrencyCheck =
            currentVersion.Length == 8;

        var updatedTrip =
            changed || requiresSqlConcurrencyCheck
                ? await _repository
                    .ReorderStopsWithConcurrencyAsync(
                        trip,
                        request.StopIds,
                        request.ExpectedRowVersion,
                        cancellationToken)
                : trip;

        // STEP 8 - Build ordered response.
        var stops = updatedTrip.Stops
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
            updatedTrip.Id,
            updatedTrip.TripName,
            updatedTrip.StartDate,
            updatedTrip.EndDate,
            updatedTrip.BudgetLkr,
            updatedTrip.VehicleType,
            updatedTrip.Headcount,
            stops)
        {
            RowVersion = updatedTrip.RowVersion.ToArray()
        };
    }
}
