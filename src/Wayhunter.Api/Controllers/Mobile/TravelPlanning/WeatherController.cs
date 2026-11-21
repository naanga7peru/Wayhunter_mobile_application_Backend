using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Wayhunter.Api.Contracts;
using Wayhunter.Application.Modules.TravelPlanning.Queries;
using Wayhunter.Infrastructure.Integrations.Maps;

namespace Wayhunter.Api.Controllers.Mobile.TravelPlanning;

[ApiController]
[Authorize]
[Route("api/mobile/trip-plans/weather")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class WeatherController(
    WeatherCacheService weather,
    WeatherForecastService forecast,
    [FromKeyedServices("weather")] InMemoryTravelProviderQuota quota) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] double? latitude, [FromQuery] double? longitude, CancellationToken cancellationToken)
    {
        if (CheckRequest(latitude, longitude) is { } error)
            return error;

        var result = await weather.GetAsync(latitude!.Value, longitude!.Value, cancellationToken);
        if (result.Status == WeatherLookupStatus.Unavailable)
            return Unavailable();

        return Ok(new
        {
            status = result.Status == WeatherLookupStatus.Fresh ? "fresh" : "stale",
            result.Observation,
            result.CachedAtUtc,
            result.FromCache
        });
    }

    [HttpGet("forecast")]
    public async Task<IActionResult> Forecast(
        [FromQuery] double? latitude, [FromQuery] double? longitude, CancellationToken cancellationToken)
    {
        if (CheckRequest(latitude, longitude) is { } error)
            return error;

        var result = await forecast.GetAsync(latitude!.Value, longitude!.Value, cancellationToken);
        return result is null ? Unavailable() : Ok(new
        {
            result.Status, result.Latitude, result.Longitude, result.RetrievedAtUtc,
            providerName = "OpenWeatherMap",
            result.Forecasts
        });
    }

    private IActionResult? CheckRequest(double? latitude, double? longitude)
    {
        if (!TrustedIdentityAccessContract.TryResolveUserId(User, out var userId))
            return Unauthorized();

        if (latitude is not double lat || longitude is not double lon ||
            !double.IsFinite(lat) || !double.IsFinite(lon) ||
            lat is < -90 or > 90 || lon is < -180 or > 180)
            return BadRequest(new { code = "INVALID_COORDINATES" });

        return quota.TryAcquire(userId, 60) ? null :
            StatusCode(StatusCodes.Status429TooManyRequests, new { code = "WEATHER_DAILY_LIMIT_REACHED" });
    }

    private IActionResult Unavailable() =>
        StatusCode(StatusCodes.Status503ServiceUnavailable, new { code = "WEATHER_UNAVAILABLE" });
}
