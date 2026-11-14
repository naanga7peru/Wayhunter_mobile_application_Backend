
using System;
using System.Collections.Generic;

namespace Wayhunter.Application.Modules.TravelPlanning.Dtos;

// Input provided by the traveler.
public sealed record AiTripItineraryRequest(
    string StartLocation,
    string Destination,
    DateOnly StartDate,
    DateOnly EndDate,
    decimal BudgetLkr,
    string VehicleType,
    int Headcount,
    string? Preferences
);

// One place suggested by the AI.
public sealed record AiTripItineraryStop(
    string PlaceName,
    string? ProviderPlaceId,
    TimeOnly? ArrivalTime,
    decimal EstimatedCostLkr
);

// Stops grouped by travel date.
public sealed record AiTripItineraryDay(
    DateOnly Date,
    IReadOnlyList<AiTripItineraryStop> Stops
);

// Proposal returned by the AI provider.
public sealed record AiTripItineraryResponse(
    IReadOnlyList<AiTripItineraryDay> Days,
    decimal EstimatedTotalCostLkr,
    bool RequiresUserReview
);
