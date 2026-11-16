
using System;

namespace Wayhunter.Application.Modules.TravelPlanning.Dtos;

public sealed record UpdateTripPlanDto(
    string TripName,
    DateOnly StartDate,
    DateOnly EndDate,
    decimal BudgetLkr,
    string VehicleType,
    int Headcount)
{
    // Client's last received SQL Server RowVersion.
    // Required for optimistic concurrency validation
    // when the persistence implementation is integrated.
    public byte[]? ExpectedRowVersion { get; init; }
}
