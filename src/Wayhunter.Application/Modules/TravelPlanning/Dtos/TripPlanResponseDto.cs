using System;
using System.Collections.Generic;
using System.Text;


namespace Wayhunter.Application.Modules.TravelPlanning.Dtos;

public sealed record TripPlanResponseDto(
    Guid TripPlanId,
    string TripName,
    DateOnly StartDate,
    DateOnly EndDate,
    decimal BudgetLkr,
    string VehicleType,
    int Headcount,
    IReadOnlyList<TripStopResponseDto> Stops
);

public sealed record TripStopResponseDto(
    Guid StopId,
    int OrderIndex,
    string Name,
    string? PlaceId,
    double? Latitude,
    double? Longitude
);

