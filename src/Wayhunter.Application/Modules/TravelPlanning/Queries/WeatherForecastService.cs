using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.TravelPlanning.Interfaces;

namespace Wayhunter.Application.Modules.TravelPlanning.Queries;

public sealed record WeatherForecastLookup(
    string Status, double Latitude, double Longitude,
    DateTimeOffset RetrievedAtUtc, IReadOnlyList<WeatherForecastPoint> Forecasts);

public sealed class WeatherForecastService(
    IWeatherForecastProvider provider, TimeSpan maximumAge, TimeProvider clock)
{
    private readonly BoundedWeatherCache<IReadOnlyList<WeatherForecastPoint>> _cache = new(256);

    public async Task<WeatherForecastLookup?> GetAsync(
        double latitude, double longitude, CancellationToken cancellationToken)
    {
        WeatherCacheService.ValidateCoordinates(latitude, longitude);
        cancellationToken.ThrowIfCancellationRequested();
        var key = (latitude, longitude);
        var now = clock.GetUtcNow();
        var cached = _cache.Read(key);
        if (cached is { } fresh && now - fresh.CachedAtUtc <= maximumAge &&
            fresh.Value.Any(point => point.ForecastAtUtc > now))
        {
            return Build("fresh", fresh.Value, fresh.CachedAtUtc, now);
        }

        var points = await provider.GetForecastAsync(latitude, longitude, cancellationToken);
        now = clock.GetUtcNow();
        if (points is { Count: > 0 and <= 40 } &&
            points.All(point => point is not null && double.IsFinite(point.TemperatureCelsius) &&
                                !string.IsNullOrWhiteSpace(point.Condition)) &&
            points.Select(point => point.ForecastAtUtc).Distinct().Count() == points.Count)
        {
            var future = points.Where(point => point.ForecastAtUtc > now &&
                                               point.ForecastAtUtc <= now.AddDays(5))
                .OrderBy(point => point.ForecastAtUtc).ToArray();
            if (future.Length > 0)
            {
                _cache.Write(key, future, now);
                return Build("fresh", future, now, now);
            }
        }

        cached = _cache.Read(key);
        return cached is { } stale && now - stale.CachedAtUtc <= TimeSpan.FromHours(24) &&
               stale.Value.Any(point => point.ForecastAtUtc > now)
            ? Build("stale", stale.Value, stale.CachedAtUtc, now)
            : null;

        WeatherForecastLookup Build(string status, IReadOnlyList<WeatherForecastPoint> values,
            DateTimeOffset retrieved, DateTimeOffset checkedAt) =>
            new(status, latitude, longitude, retrieved,
                values.Where(point => point.ForecastAtUtc > checkedAt).ToArray());
    }
}
