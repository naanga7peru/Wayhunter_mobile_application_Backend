
using System;
using System.Collections.Generic;
using System.Linq;
using Wayhunter.Application.Modules.TravelPlanning.Dtos;
using Wayhunter.Application.Modules.TravelPlanning.Interfaces;

namespace Wayhunter.Application.Modules.TravelPlanning.Validators;

public enum AiPlaceMatchStatus
{
    MissingProviderId,
    UnknownProviderId,
    DetailsMismatch,
    ExternalMatchUnverified
}

public sealed record AiTripPlaceReview(
    DateOnly Date,
    int StopIndex,
    string PlaceName,
    AiPlaceMatchStatus Status,
    bool RequiresUserReview
);

public static class AiTripPlaceReviewEvaluator
{
    public static IReadOnlyList<AiTripPlaceReview> Evaluate(
        AiTripItineraryRequest request,
        AiTripItineraryResponse response,
        IReadOnlyList<ExternalPlaceSuggestion> providerPlaces)
    {
        // Validate AI itinerary first.
        AiTripItineraryValidator.ValidateResponse(
            request,
            response);

        ArgumentNullException.ThrowIfNull(providerPlaces);

        var reviews = new List<AiTripPlaceReview>();

        foreach (var day in response.Days)
        {
            for (int i = 0; i < day.Stops.Count; i++)
            {
                var stop = day.Stops[i];

                AiPlaceMatchStatus status;

                // Step 1: Check whether AI provided a Place ID.
                if (string.IsNullOrWhiteSpace(
                    stop.ProviderPlaceId))
                {
                    status = AiPlaceMatchStatus.MissingProviderId;
                }
                else
                {
                    // Step 2: Match the AI Place ID
                    // against external provider results.
                    var match = providerPlaces.FirstOrDefault(
                        place =>
                            place is not null &&
                            string.Equals(
                                place.ProviderPlaceId,
                                stop.ProviderPlaceId,
                                StringComparison.Ordinal));

                    if (match is null)
                    {
                        status = AiPlaceMatchStatus.UnknownProviderId;
                    }
                    else if (
                        !string.Equals(
                            match.Name?.Trim(),
                            stop.PlaceName.Trim(),
                            StringComparison.OrdinalIgnoreCase) ||
                        !double.IsFinite(match.Latitude) ||
                        !double.IsFinite(match.Longitude) ||
                        match.Latitude < -90 ||
                        match.Latitude > 90 ||
                        match.Longitude < -180 ||
                        match.Longitude > 180)
                    {
                        // Step 3: Check name and coordinates.
                        status = AiPlaceMatchStatus.DetailsMismatch;
                    }
                    else
                    {
                        // Matching an external provider does NOT
                        // mean the place is Hunter-approved.
                        status =
                            AiPlaceMatchStatus.ExternalMatchUnverified;
                    }
                }

                // Every AI-generated place needs review.
                reviews.Add(new AiTripPlaceReview(
                    day.Date,
                    i,
                    stop.PlaceName,
                    status,
                    RequiresUserReview: true));
            }
        }

        return reviews;
    }
}
