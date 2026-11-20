using System;
using System.Collections.Generic;
using System.Text;


namespace Wayhunter.Application.Modules.TravelPlanning.Dtos;

public sealed record CreateTripPlanDto(
    string TripName,
    DateOnly StartDate,
    DateOnly EndDate,
    decimal BudgetLkr,
    string VehicleType,
    int Headcount
)
{
    // Ordered stops explicitly selected by the traveler after reviewing the proposal.
    public IReadOnlyList<InitialTripStopDto?>? InitialStops { get; init; }
}

public sealed record InitialTripStopDto(
    string Name,
    string? PlaceId = null,
    double? Latitude = null,
    double? Longitude = null
);

