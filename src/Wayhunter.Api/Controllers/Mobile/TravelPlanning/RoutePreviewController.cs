
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Wayhunter.Api.Contracts;
using Wayhunter.Application.Modules.TravelPlanning.Dtos;
using Wayhunter.Application.Modules.TravelPlanning.Queries;
using Wayhunter.Infrastructure.Integrations.Maps;

namespace Wayhunter.Api.Controllers.Mobile.TravelPlanning;

[ApiController]
[Authorize]
[Route("api/mobile/trip-plans/routes")]
public sealed class RoutePreviewController : ControllerBase
{
    private readonly PreviewTripRouteService _service;
    private readonly InMemoryRoutePreviewDailyQuota _quota;
    private readonly IOptions<MapsProviderOptions> _options;

    public RoutePreviewController(
        PreviewTripRouteService service,
        InMemoryRoutePreviewDailyQuota quota,
        IOptions<MapsProviderOptions> options)
    {
        _service = service ??
            throw new ArgumentNullException(nameof(service));

        _quota = quota ??
            throw new ArgumentNullException(nameof(quota));

        _options = options ??
            throw new ArgumentNullException(nameof(options));
    }

    // POST /api/mobile/trip-plans/routes/preview
    [HttpPost("preview")]
    [RequestSizeLimit(16 * 1024)]
    [ResponseCache(NoStore = true)]
    public async Task<IActionResult> Preview(
        [FromBody] RoutePreviewRequest? request,
        CancellationToken cancellationToken)
    {
        if (!TrustedIdentityAccessContract.TryResolveUserId(
                User, out var userId))
        {
            return Unauthorized();
        }

        try
        {
            PreviewTripRouteService.ValidateOrderedStops(
                request?.OrderedStops);
        }
        catch (ArgumentException)
        {
            return BadRequest(new
            {
                code = "INVALID_ROUTE_REQUEST"
            });
        }

        var settings = _options.Value;

        if (!settings.EnableRoutes ||
            settings.Validate().Count > 0)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { code = "ROUTES_PROVIDER_UNAVAILABLE" });
        }

        if (!_quota.TryAcquire(userId))
        {
            return StatusCode(
                StatusCodes.Status429TooManyRequests,
                new { code = "ROUTE_DAILY_LIMIT_REACHED" });
        }

        try
        {
            var result = await _service.PreviewAsync(
                request!.OrderedStops,
                cancellationToken);

            if (!result.IsSuccess)
            {
                return StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    new { code = "ROUTES_PROVIDER_UNAVAILABLE" });
            }

            // Preview only: no trip is saved or started.
            return Ok(new
            {
                routeGeometry = result.RouteGeometry,
                distanceKm = result.DistanceKm,
                estimatedDurationMinutes =
                    result.EstimatedDurationMinutes,
                providerName = result.ProviderName,
                trafficAware = false
            });
        }
        catch (InvalidOperationException)
        {
            return StatusCode(
                StatusCodes.Status502BadGateway,
                new { code = "INVALID_ROUTE_RESPONSE" });
        }
    }
}
