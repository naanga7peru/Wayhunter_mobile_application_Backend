
using System;
using Wayhunter.Application.Modules.TravelPlanning.Interfaces;

namespace Wayhunter.Application.Modules.TravelPlanning.Validators;

public static class ProviderResultValidator
{
    // Validate external Places provider results.
    public static void Validate(PlacesProviderResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.Places is null)
            throw new InvalidOperationException(
                "Places collection cannot be null.");

        if (!result.IsSuccess)
        {
            if (result.Places.Count > 0)
                throw new InvalidOperationException(
                    "Failed Places requests cannot return places.");

            RequireFailureReason(result.FailureReason);
            return;
        }

        EnsureNoFailureReason(result.FailureReason);
    }

    // Validate external Routing provider results.
    public static void Validate(RouteProviderResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (!result.IsSuccess)
        {
            if (result.RouteGeometry is { Count: > 0 } ||
                result.DistanceKm.HasValue ||
                result.EstimatedDurationMinutes.HasValue)
            {
                throw new InvalidOperationException(
                    "Failed route requests cannot return " +
                    "geometry, distance or ETA.");
            }

            RequireFailureReason(result.FailureReason);
            return;
        }

        if (result.RouteGeometry is null ||
            result.RouteGeometry.Count < 2)
        {
            throw new InvalidOperationException(
                "Successful routes require valid geometry.");
        }

        if (result.DistanceKm is < 0 ||
            result.EstimatedDurationMinutes is < 0)
        {
            throw new InvalidOperationException(
                "Route distance and duration cannot be negative.");
        }

        EnsureNoFailureReason(result.FailureReason);
    }

    // Validate external Weather provider results.
    public static void Validate(WeatherProviderResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (!result.IsSuccess)
        {
            if (result.Observation is not null)
                throw new InvalidOperationException(
                    "Failed weather requests cannot " +
                    "return weather observations.");

            RequireFailureReason(result.FailureReason);
            return;
        }

        if (result.Observation is null)
            throw new InvalidOperationException(
                "Successful weather requests require " +
                "a weather observation.");

        EnsureNoFailureReason(result.FailureReason);
    }

    private static void RequireFailureReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException(
                "Provider failures require an error reason.");
    }

    private static void EnsureNoFailureReason(string? reason)
    {
        if (!string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException(
                "Successful provider results cannot " +
                "contain a failure reason.");
    }
}
