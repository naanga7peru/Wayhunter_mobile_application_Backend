using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Wayhunter.Infrastructure.Integrations.Maps;
using Wayhunter.Api.Contracts;
using Wayhunter.Application.Modules.TravelPlanning.Interfaces;

namespace Wayhunter.Api.Controllers.Mobile.TravelPlanning;

[ApiController]
[Authorize]
[Route("api/mobile/trip-plans/places")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PlacePickerController(
    IPlacesProvider places,
    IPlaceDetailsProvider details,
    [FromKeyedServices("place-picker")] InMemoryTravelProviderQuota quota) : ControllerBase
{
    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string? query, CancellationToken cancellationToken)
    {
        if (!TrustedIdentityAccessContract.TryResolveUserId(User, out var userId))
            return Unauthorized();

        var text = query?.Trim();
        if (string.IsNullOrEmpty(text) || text.Length > 200 || text.Any(char.IsControl))
            return BadRequest(new { code = "INVALID_PLACE_QUERY" });

        if (!quota.TryAcquire(userId, 60))
            return QuotaExceeded();

        var result = await places.SearchAsync(text, cancellationToken);
        return result.IsSuccess ? Ok(result.Places.Select(ToExternalPlace)) : Unavailable();
    }

    [HttpGet("{providerPlaceId}")]
    public async Task<IActionResult> Details(string providerPlaceId, CancellationToken cancellationToken)
    {
        if (!TrustedIdentityAccessContract.TryResolveUserId(User, out var userId))
            return Unauthorized();

        if (providerPlaceId.Length > 512 ||
            providerPlaceId.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '-' and not '_'))
        {
            return BadRequest(new { code = "INVALID_PLACE_ID" });
        }

        if (!quota.TryAcquire(userId, 60))
            return QuotaExceeded();

        var result = await details.GetDetailsAsync(providerPlaceId, cancellationToken);
        if (!result.IsSuccess)
            return Unavailable();

        return result.Places.Count == 0
            ? NotFound()
            : Ok(ToExternalPlace(result.Places[0]));
    }

    private IActionResult QuotaExceeded() =>
        StatusCode(StatusCodes.Status429TooManyRequests, new { code = "PLACE_PICKER_DAILY_LIMIT_REACHED" });

    private IActionResult Unavailable() => StatusCode(StatusCodes.Status503ServiceUnavailable,
        new { code = "PLACES_PROVIDER_UNAVAILABLE" });

    private static object ToExternalPlace(ExternalPlaceSuggestion place) => new
    {
        place.ProviderPlaceId,
        place.Name,
        place.Latitude,
        place.Longitude,
        place.ProviderName,
        isVerified = false
    };
}
