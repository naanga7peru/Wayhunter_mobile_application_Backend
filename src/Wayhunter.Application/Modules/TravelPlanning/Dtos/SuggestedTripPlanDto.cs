using System;

namespace Wayhunter.Application.Modules.TravelPlanning.Dtos;

// A privacy-minimal card. No owner identity, row version, raw GPS or memories.
public sealed record SuggestedTripPlanDto(
    Guid TripPlanId,
    string TripName,
    DateOnly StartDate,
    DateOnly EndDate,
    decimal BudgetLkr,
    string VehicleType,
    int Headcount,
    int StopCount,
    DateTimeOffset CompletedAtUtc);
