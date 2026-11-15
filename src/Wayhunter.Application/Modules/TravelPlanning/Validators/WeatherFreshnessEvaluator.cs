
using System;
using Wayhunter.Application.Modules.TravelPlanning.Interfaces;

namespace Wayhunter.Application.Modules.TravelPlanning.Validators;

public sealed record WeatherFreshnessResult(
    WeatherObservation Observation,
    DateTimeOffset CachedAtUtc,
    DateTimeOffset CheckedAtUtc,
    bool IsStale
);

public static class WeatherFreshnessEvaluator
{
    public static WeatherFreshnessResult Evaluate(
        WeatherObservation observation,
        DateTimeOffset cachedAtUtc,
        DateTimeOffset checkedAtUtc,
        TimeSpan maximumAge)
    {
        ArgumentNullException.ThrowIfNull(observation);

        if (maximumAge <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumAge),
                "Maximum age must be positive.");
        }

        if (cachedAtUtc > checkedAtUtc)
        {
            throw new ArgumentException(
                "Cache timestamp cannot be in the future.");
        }

        if (observation.ObservedAtUtc > checkedAtUtc)
        {
            throw new ArgumentException(
                "Observation timestamp cannot be in the future.");
        }

        if (!double.IsFinite(observation.Latitude) ||
            observation.Latitude < -90 ||
            observation.Latitude > 90 ||
            !double.IsFinite(observation.Longitude) ||
            observation.Longitude < -180 ||
            observation.Longitude > 180)
        {
            throw new ArgumentException(
                "Weather observation has invalid coordinates.");
        }

        if (!double.IsFinite(observation.TemperatureCelsius))
        {
            throw new ArgumentException(
                "Weather temperature must be finite.");
        }

        if (string.IsNullOrWhiteSpace(observation.ProviderName) ||
            string.IsNullOrWhiteSpace(observation.Condition))
        {
            throw new ArgumentException(
                "Weather source and condition are required.");
        }

        var observationAge =
            checkedAtUtc - observation.ObservedAtUtc;

        var cacheAge =
            checkedAtUtc - cachedAtUtc;

        var isStale =
            observationAge > maximumAge ||
            cacheAge > maximumAge;

        return new WeatherFreshnessResult(
            observation,
            cachedAtUtc,
            checkedAtUtc,
            isStale);
    }
}
