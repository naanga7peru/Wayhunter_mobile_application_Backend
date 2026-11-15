
using System;

namespace Wayhunter.Application.Modules.TravelPlanning.Validators;

// Request for nearby place discovery.
public sealed record NearbySearchRequest(
    double Latitude,
    double Longitude,
    NearbyPlaceCategory Category
);

public static class NearbySearchRequestValidator
{
    // WAYHUNTER requirement:
    // Nearby Find searches within a 1 km radius.
    public const int SearchRadiusMeters = 1000;

    public static void Validate(
        NearbySearchRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!double.IsFinite(request.Latitude) ||
            request.Latitude < -90 ||
            request.Latitude > 90)
        {
            throw new ArgumentException(
                "Invalid GPS latitude.");
        }

        if (!double.IsFinite(request.Longitude) ||
            request.Longitude < -180 ||
            request.Longitude > 180)
        {
            throw new ArgumentException(
                "Invalid GPS longitude.");
        }

        if (!Enum.IsDefined(request.Category))
        {
            throw new ArgumentException(
                "Unsupported nearby category.");
        }
    }

    public static string GetProviderSearchTerm(
        NearbySearchRequest request)
    {
        Validate(request);

        return NearbyPlaceCategoryMapper.ToSearchTerm(
            request.Category);
    }
}
