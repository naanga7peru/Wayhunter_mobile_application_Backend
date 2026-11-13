
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

        // Step 1: Validate the submitted data
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

        // Step 2: Create the domain entity
        var tripPlan = new TripPlan(
            ownerUserId,
            request.TripName,
            request.StartDate,
            request.EndDate,
            request.BudgetLkr,
            request.VehicleType,
            request.Headcount);

        // Step 3: Save using the repository
        await _repository.AddAsync(
            tripPlan,
            cancellationToken);

        // Step 4: Return response data
        var stops = tripPlan.Stops
            .Select(stop => new TripStopResponseDto(
                stop.Id,
                stop.OrderIndex,
                stop.Name,
                stop.PlaceId,
                stop.Latitude,
                stop.Longitude))
            .ToList();

        return new TripPlanResponseDto(
            tripPlan.Id,
            tripPlan.TripName,
            tripPlan.StartDate,
            tripPlan.EndDate,
            tripPlan.BudgetLkr,
            tripPlan.VehicleType,
            tripPlan.Headcount,
            stops);
    }
}
