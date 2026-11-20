
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Wayhunter.Api.Contracts;
using Wayhunter.Application.Modules.TravelPlanning.Queries;
using Wayhunter.Application.Modules.TravelPlanning.Validators;
using Wayhunter.Infrastructure.Integrations.Maps;

namespace Wayhunter.Api.Controllers.Mobile.TravelPlanning;

[ApiController]
[Authorize]
[Route("api/mobile/trip-plans")]
public sealed class NearbyPlacesController : ControllerBase
{
    private readonly NearbyPlaceSearchService _service;
    private readonly InMemoryNearbyPlacesDailyQuota _quota;
    private readonly IOptions<MapsProviderOptions> _options;

    public NearbyPlacesController(
        NearbyPlaceSearchService service,
        InMemoryNearbyPlacesDailyQuota quota,
        IOptions<MapsProviderOptions> options)
    {
        _service = service ??
            throw new ArgumentNullException(nameof(service));

        _quota = quota ??
            throw new ArgumentNullException(nameof(quota));

        _options = options ??
            throw new ArgumentNullException(nameof(options));
    }

    // GET /api/mobile/trip-plans/nearby
    // ?latitude=9.66&longitude=80.02&category=food
    [HttpGet("nearby")]
    [ResponseCache(
        NoStore = true,
        Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> FindNearby(
        [FromQuery] double? latitude,
        [FromQuery] double? longitude,
        [FromQuery] string? category,
        CancellationToken cancellationToken)
    {
        if (!TrustedIdentityAccessContract.TryResolveUserId(
                User, out var userId))
        {
            return Unauthorized();
        }

        if (!latitude.HasValue ||
            !longitude.HasValue ||
            string.IsNullOrWhiteSpace(category))
        {
            return BadRequest(new
            {
                code = "INVALID_NEARBY_REQUEST"
            });
        }

        NearbySearchRequest request;

        try
        {
            var selectedCategory =
                NearbyPlaceCategoryMapper.FromClientValue(category);

            request = new NearbySearchRequest(
                latitude.Value,
                longitude.Value,
                selectedCategory);

            NearbySearchRequestValidator.Validate(request);
        }
        catch (ArgumentException)
        {
            return BadRequest(new
            {
                code = "INVALID_NEARBY_REQUEST"
            });
        }

        var settings = _options.Value;

        if (!settings.EnablePlaces ||
            settings.Validate().Count > 0)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { code = "NEARBY_PROVIDER_UNAVAILABLE" });
        }

        if (!_quota.TryAcquire(userId))
        {
            return StatusCode(
                StatusCodes.Status429TooManyRequests,
                new { code = "NEARBY_DAILY_LIMIT_REACHED" });
        }

        try
        {
            var result = await _service.SearchAsync(
                request, cancellationToken);

            if (!result.IsSuccess)
            {
                return StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    new { code = "NEARBY_PROVIDER_UNAVAILABLE" });
            }

            // Google results are suggestions,
            // NOT M4 Hunter-approved locations.
            return Ok(new
            {
                radiusMeters = 1000,
                hunterVerified = false,
                places = result.Places
            });
        }
        catch (InvalidOperationException)
        {
            return StatusCode(
                StatusCodes.Status502BadGateway,
                new { code = "INVALID_NEARBY_RESPONSE" });
        }
    }
}
