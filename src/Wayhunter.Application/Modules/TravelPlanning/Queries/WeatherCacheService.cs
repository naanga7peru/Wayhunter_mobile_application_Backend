
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.TravelPlanning.Interfaces;
using Wayhunter.Application.Modules.TravelPlanning.Validators;

namespace Wayhunter.Application.Modules.TravelPlanning.Queries;

public enum WeatherLookupStatus
{
    Fresh,
    Stale,
    Unavailable
}

public sealed record WeatherLookupResult(
    WeatherLookupStatus Status,
    WeatherObservation? Observation,
    DateTimeOffset? CachedAtUtc,
    string? FailureReason,
    bool FromCache
);

public sealed class WeatherCacheService
{
    private sealed record CachedWeather(
        WeatherObservation Observation,
        DateTimeOffset CachedAtUtc
    );

    private readonly IWeatherProvider _provider;
    private readonly TimeProvider _clock;
    private readonly TimeSpan _maximumAge;

    private readonly ConcurrentDictionary<
        (double Latitude, double Longitude),
        CachedWeather> _cache = new();

    public WeatherCacheService(
        IWeatherProvider provider,
        TimeSpan maximumAge,
        TimeProvider? clock = null)
    {
        _provider = provider
            ?? throw new ArgumentNullException(nameof(provider));

        if (maximumAge <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumAge));
        }

        _maximumAge = maximumAge;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<WeatherLookupResult> GetAsync(
        double latitude,
        double longitude,
        CancellationToken cancellationToken = default)
    {
        ValidateCoordinates(latitude, longitude);

        cancellationToken.ThrowIfCancellationRequested();

        var key = (latitude, longitude);
        var now = _clock.GetUtcNow();

        var hasCached = _cache.TryGetValue(
            key, out var cached);

        // Return valid cached weather without another API call.
        if (hasCached && cached is not null)
        {
            var freshness =
                WeatherFreshnessEvaluator.Evaluate(
                    cached.Observation,
                    cached.CachedAtUtc,
                    now,
                    _maximumAge);

            if (!freshness.IsStale)
            {
                return new WeatherLookupResult(
                    WeatherLookupStatus.Fresh,
                    cached.Observation,
                    cached.CachedAtUtc,
                    null,
                    true);
            }
        }

        // Cache missing or stale: request real provider data.
        var response = await _provider.GetCurrentWeatherAsync(
            latitude,
            longitude,
            cancellationToken);

        ProviderResultValidator.Validate(response);

        if (response.IsSuccess)
        {
            now = _clock.GetUtcNow();

            var observation = response.Observation!;

            var freshness =
                WeatherFreshnessEvaluator.Evaluate(
                    observation,
                    now,
                    now,
                    _maximumAge);

            _cache[key] = new CachedWeather(
                observation,
                now);

            return new WeatherLookupResult(
                freshness.IsStale
                    ? WeatherLookupStatus.Stale
                    : WeatherLookupStatus.Fresh,
                observation,
                now,
                null,
                false);
        }

        // Provider failed, but previous weather exists.
        // Never label old weather as fresh.
        if (hasCached && cached is not null)
        {
            return new WeatherLookupResult(
                WeatherLookupStatus.Stale,
                cached.Observation,
                cached.CachedAtUtc,
                response.FailureReason,
                true);
        }

        // No real weather available.
        return new WeatherLookupResult(
            WeatherLookupStatus.Unavailable,
            null,
            null,
            response.FailureReason,
            false);
    }

    private static void ValidateCoordinates(
        double latitude,
        double longitude)
    {
        if (!double.IsFinite(latitude) ||
            latitude < -90 ||
            latitude > 90)
        {
            throw new ArgumentOutOfRangeException(
                nameof(latitude));
        }

        if (!double.IsFinite(longitude) ||
            longitude < -180 ||
            longitude > 180)
        {
            throw new ArgumentOutOfRangeException(
                nameof(longitude));
        }
    }
}
