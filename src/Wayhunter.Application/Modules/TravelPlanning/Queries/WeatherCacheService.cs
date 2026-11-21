
using System;
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
    private readonly IWeatherProvider _provider;
    private readonly TimeProvider _clock;
    private readonly TimeSpan _maximumAge;

    private readonly BoundedWeatherCache<WeatherObservation> _cache = new(256);

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

        var cached = _cache.Read(key);

        // Return valid cached weather without another API call.
        if (cached is { } entry)
        {
            var freshness =
                WeatherFreshnessEvaluator.Evaluate(
                    entry.Value,
                    entry.CachedAtUtc,
                    now,
                    _maximumAge);

            if (!freshness.IsStale)
            {
                return new WeatherLookupResult(
                    WeatherLookupStatus.Fresh,
                    entry.Value,
                    entry.CachedAtUtc,
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

        if (response.IsSuccess &&
            _clock.GetUtcNow() - response.Observation!.ObservedAtUtc <= TimeSpan.FromHours(24))
        {
            now = _clock.GetUtcNow();

            var observation = response.Observation!;

            var freshness =
                WeatherFreshnessEvaluator.Evaluate(
                    observation,
                    now,
                    now,
                    _maximumAge);

            _cache.Write(key, observation, now);

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
        // Never label old weather as fresh or return observations older than a day.
        now = _clock.GetUtcNow();
        if (_cache.Read(key) is { } fallback && now - fallback.Value.ObservedAtUtc <= TimeSpan.FromHours(24))
        {
            return new WeatherLookupResult(
                WeatherLookupStatus.Stale,
                fallback.Value,
                fallback.CachedAtUtc,
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

    internal static void ValidateCoordinates(
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
