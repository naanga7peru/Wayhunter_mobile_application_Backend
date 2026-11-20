
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.TravelPlanning.Dtos;
using Wayhunter.Application.Modules.TravelPlanning.Interfaces;
using Wayhunter.Application.Modules.TravelPlanning.Validators;
using Wayhunter.Domain.Modules.TravelPlanning.Entities;

namespace Wayhunter.Application.Modules.TravelPlanning.Commands;

public sealed class CreateTripPlanService
{
    private readonly ITripPlanRepository _repository;

    public CreateTripPlanService(ITripPlanRepository repository)
    {
        _repository = repository
            ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<TripPlanResponseDto> CreateAsync(
        CreateTripPlanDto request,
        string ownerUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(ownerUserId))
        {
            throw new ArgumentException(
                "Authenticated user ID is required.",
                nameof(ownerUserId));
        }

        // Step 1: Validate submitted data.
        var errors = TripPlanValidator.ValidateCreate(
            request.TripName,
            request.StartDate,
            request.EndDate,
            request.BudgetLkr,
            request.VehicleType,
            request.Headcount);

        if (errors.Count > 0)
        {
            throw new ArgumentException(
                string.Join("; ", errors),
                nameof(request));
        }

        // Step 2: Create domain entity.
        var tripPlan = new TripPlan(
            ownerUserId,
            request.TripName,
            request.StartDate,
            request.EndDate,
            request.BudgetLkr,
            request.VehicleType,
            request.Headcount);

        if (request.InitialStops is { } initialStops)
        {
            if (initialStops.Count > 100)
                throw new ArgumentException("A draft can be created with up to 100 stops.");

            var seen = new System.Collections.Generic.HashSet<(string, string?, double?, double?)>();
            foreach (var stop in initialStops)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (stop is null)
                    throw new ArgumentException("Initial stops are required to be non-null.");

                var added = tripPlan.AddStop(stop.Name, stop.PlaceId, stop.Latitude, stop.Longitude);
                if (!seen.Add((added.Name.ToUpperInvariant(), added.PlaceId, added.Latitude, added.Longitude)))
                    throw new ArgumentException("Initial stops must be distinct.");
            }
        }

        // Save the draft and its ordered stops together.
        await _repository.AddAsync(
            tripPlan,
            cancellationToken);

        // Step 4: Map stops.
        var stops = tripPlan.Stops
            .OrderBy(stop => stop.OrderIndex)
            .Select(stop => new TripStopResponseDto(
                stop.Id,
                stop.OrderIndex,
                stop.Name,
                stop.PlaceId,
                stop.Latitude,
                stop.Longitude))
            .ToList();

        // Step 5: Return response with current RowVersion.
        return new TripPlanResponseDto(
            tripPlan.Id,
            tripPlan.TripName,
            tripPlan.StartDate,
            tripPlan.EndDate,
            tripPlan.BudgetLkr,
            tripPlan.VehicleType,
            tripPlan.Headcount,
            stops)
        {
            RowVersion = tripPlan.RowVersion.ToArray()
        };
    }
}
