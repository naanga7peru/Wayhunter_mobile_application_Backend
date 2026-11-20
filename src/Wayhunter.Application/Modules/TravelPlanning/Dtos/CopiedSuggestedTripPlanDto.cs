using System;
using System.Collections.Generic;

namespace Wayhunter.Application.Modules.TravelPlanning.Dtos;

// The creator ID is recorded on the server but not returned to the client.
// No memories, M3 GPS samples or original RowVersion are exposed.
public sealed record CopiedSuggestedTripPlanDto(
    Guid TripPlanId,
    Guid SourceTripPlanId,
    string TripName,
    DateOnly StartDate,
    DateOnly EndDate,
    decimal BudgetLkr,
    string VehicleType,
    int Headcount,
    IReadOnlyList<TripStopResponseDto> Stops)
{
    public byte[] RowVersion { get; init; } = Array.Empty<byte>();
}
