
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.TravelPlanning.Interfaces;
using Wayhunter.Application.Modules.TravelPlanning.Validators;

namespace Wayhunter.Application.Modules.TravelPlanning.Queries;

public sealed class NearbyPlaceSearchService
{
    private const double EarthRadiusMeters = 6371000;

    private readonly INearbyPlacesProvider _provider;

    public NearbyPlaceSearchService(
        INearbyPlacesProvider provider)
    {
        _provider = provider
            ?? throw new ArgumentNullException(nameof(provider));
    }

    public async Task<PlacesProviderResult> SearchAsync(
        NearbySearchRequest request,
        CancellationToken cancellationToken = default)
    {
        // Step 1: Validate GPS and category.
        NearbySearchRequestValidator.Validate(request);

        // Step 2: Convert category to provider search term.
        var searchTerm =
            NearbySearchRequestValidator.GetProviderSearchTerm(
                request);

        // Step 3: Ask provider to search within 1 km.
        var result = await _provider.SearchNearbyAsync(
            request.Latitude,
            request.Longitude,
            NearbySearchRequestValidator.SearchRadiusMeters,
            searchTerm,
            cancellationToken);

        // Step 4: Reject invalid provider responses.
        ProviderResultValidator.Validate(result);

        if (!result.IsSuccess)
        {
            return result;
        }

        // Step 5: Keep valid places within 1 km.
        var nearbyPlaces = result.Places
            .Where(place =>
                place is not null &&
                !string.IsNullOrWhiteSpace(place.ProviderPlaceId) &&
                !string.IsNullOrWhiteSpace(place.Name) &&
                IsValidCoordinates(
                    place.Latitude,
                    place.Longitude) &&
                CalculateDistanceMeters(
                    request.Latitude,
                    request.Longitude,
                    place.Latitude,
                    place.Longitude)
                <= NearbySearchRequestValidator.SearchRadiusMeters)
            .ToArray();

        return new PlacesProviderResult(
            true,
            nearbyPlaces,
            null);
    }

    private static bool IsValidCoordinates(
        double latitude,
        double longitude)
    {
        return double.IsFinite(latitude) &&
               double.IsFinite(longitude) &&
               latitude >= -90 &&
               latitude <= 90 &&
               longitude >= -180 &&
               longitude <= 180;
    }

    private static double CalculateDistanceMeters(
        double startLatitude,
        double startLongitude,
        double endLatitude,
        double endLongitude)
    {
        const double toRadians = Math.PI / 180.0;

        var latitudeDifference =
            (endLatitude - startLatitude) * toRadians;

        var longitudeDifference =
            (endLongitude - startLongitude) * toRadians;

        var a =
            Math.Pow(Math.Sin(latitudeDifference / 2), 2) +
            Math.Cos(startLatitude * toRadians) *
            Math.Cos(endLatitude * toRadians) *
            Math.Pow(Math.Sin(longitudeDifference / 2), 2);

        a = Math.Clamp(a, 0.0, 1.0);

        var c = 2 * Math.Atan2(
            Math.Sqrt(a),
            Math.Sqrt(1 - a));

        return EarthRadiusMeters * c;
    }
}
