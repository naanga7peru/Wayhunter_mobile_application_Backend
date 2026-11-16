
using System;
using System.Collections.Generic;

namespace Wayhunter.Application.Modules.TravelPlanning.Dtos;

public sealed record TripPlanResponseDto(
    Guid TripPlanId,
    string TripName,
    DateOnly StartDate,
    DateOnly EndDate,
    decimal BudgetLkr,
    string VehicleType,
    int Headcount,
    IReadOnlyList<TripStopResponseDto> Stops)
{
    // Latest SQL Server concurrency token.
    // Client must send this value during the next update.
    public byte[] RowVersion { get; init; } = Array.Empty<byte>();
}

public sealed record TripStopResponseDto(
    Guid StopId,
    int OrderIndex,
    string Name,
    string? PlaceId,
    double? Latitude,
    double? Longitude
);
