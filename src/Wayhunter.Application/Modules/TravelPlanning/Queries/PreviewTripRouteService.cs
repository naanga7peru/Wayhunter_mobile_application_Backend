
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.TravelPlanning.Interfaces;
using Wayhunter.Application.Modules.TravelPlanning.Validators;

namespace Wayhunter.Application.Modules.TravelPlanning.Queries;

public sealed class PreviewTripRouteService
{
    private readonly IRoutesProvider _provider;

    public PreviewTripRouteService(IRoutesProvider provider)
    {
        _provider = provider ??
            throw new ArgumentNullException(nameof(provider));
    }

    public static void ValidateOrderedStops(
        IReadOnlyList<RouteWaypoint>? stops)
    {
        if (stops is null ||
            stops.Count < 2 ||
            stops.Count > 27)
        {
            throw new ArgumentException(
                "Route requires 2 to 27 ordered stops.");
        }

        foreach (var stop in stops)
        {
            if (stop is null ||
                !IsValidCoordinates(
                    stop.Latitude,
                    stop.Longitude))
            {
                throw new ArgumentException(
                    "Invalid route coordinates.");
            }
        }
    }

    public async Task<RouteProviderResult> PreviewAsync(
        IReadOnlyList<RouteWaypoint> orderedStops,
        CancellationToken cancellationToken = default)
    {
        ValidateOrderedStops(orderedStops);

        cancellationToken.ThrowIfCancellationRequested();

        var result = await _provider.CalculateRouteAsync(
            orderedStops,
            cancellationToken);

        ProviderResultValidator.Validate(result);

        if (!result.IsSuccess)
            return result;

        // Never return invalid or invented distance / ETA.
        if (!result.DistanceKm.HasValue ||
            !result.EstimatedDurationMinutes.HasValue ||
            !double.IsFinite(result.DistanceKm.Value) ||
            !double.IsFinite(
                result.EstimatedDurationMinutes.Value) ||
            result.DistanceKm.Value < 0 ||
            result.EstimatedDurationMinutes.Value < 0 ||
            string.IsNullOrWhiteSpace(result.ProviderName) ||
            result.RouteGeometry.Count > 20000)
        {
            throw new InvalidOperationException(
                "Invalid route provider response.");
        }

        foreach (var point in result.RouteGeometry)
        {
            if (point is null ||
                !IsValidCoordinates(
                    point.Latitude,
                    point.Longitude))
            {
                throw new InvalidOperationException(
                    "Invalid route geometry.");
            }
        }

        return result;
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
}
