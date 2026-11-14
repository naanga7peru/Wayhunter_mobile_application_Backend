
using System;
using System.Collections.Generic;
using Wayhunter.Application.Modules.TravelPlanning.Dtos;

namespace Wayhunter.Application.Modules.TravelPlanning.Validators;

public static class AiTripItineraryValidator
{
    public static void ValidateRequest(
        AiTripItineraryRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.StartLocation))
            throw new ArgumentException(
                "Start location is required.");

        if (string.IsNullOrWhiteSpace(request.Destination))
            throw new ArgumentException(
                "Destination is required.");

        if (request.StartDate == default ||
            request.EndDate < request.StartDate)
            throw new ArgumentException(
                "Invalid trip dates.");

        if (request.BudgetLkr < 0)
            throw new ArgumentException(
                "Budget cannot be negative.");

        if (string.IsNullOrWhiteSpace(request.VehicleType))
            throw new ArgumentException(
                "Vehicle type is required.");

        if (request.Headcount < 1)
            throw new ArgumentException(
                "Headcount must be at least one.");
    }

    public static void ValidateResponse(
        AiTripItineraryRequest request,
        AiTripItineraryResponse response)
    {
        ValidateRequest(request);
        ArgumentNullException.ThrowIfNull(response);

        if (response.Days is null || response.Days.Count == 0)
            throw new ArgumentException(
                "AI itinerary must contain at least one day.");

        if (response.EstimatedTotalCostLkr < 0)
            throw new ArgumentException(
                "Estimated total cost cannot be negative.");

        if (!response.RequiresUserReview)
            throw new ArgumentException(
                "AI itinerary requires user review.");

        var seenDates = new HashSet<DateOnly>();
        DateOnly? previousDate = null;

        foreach (var day in response.Days)
        {
            if (day is null)
                throw new ArgumentException(
                    "AI itinerary contains an invalid day.");

            if (day.Date < request.StartDate ||
                day.Date > request.EndDate)
                throw new ArgumentException(
                    "AI itinerary day is outside trip dates.");

            if (!seenDates.Add(day.Date))
                throw new ArgumentException(
                    "Duplicate itinerary date.");

            if (previousDate.HasValue &&
                day.Date < previousDate.Value)
                throw new ArgumentException(
                    "Itinerary days must be ordered.");

            previousDate = day.Date;

            if (day.Stops is null || day.Stops.Count == 0)
                throw new ArgumentException(
                    "Each itinerary day requires stops.");

            foreach (var stop in day.Stops)
            {
                if (stop is null ||
                    string.IsNullOrWhiteSpace(stop.PlaceName))
                    throw new ArgumentException(
                        "AI stop must have a place name.");

                if (stop.EstimatedCostLkr < 0)
                    throw new ArgumentException(
                        "Stop estimated cost cannot be negative.");
            }
        }
    }
}
