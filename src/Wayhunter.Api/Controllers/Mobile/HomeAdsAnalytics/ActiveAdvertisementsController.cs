
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wayhunter.Api.Contracts.HomeAdsAnalytics;
using Wayhunter.Application.Modules.HomeAdsAnalytics.Dtos;
using Wayhunter.Application.Modules.HomeAdsAnalytics.Services;
using Wayhunter.Domain.Modules.HomeAdsAnalytics.Enums;

namespace Wayhunter.Api.Controllers.Mobile.HomeAdsAnalytics;

[ApiController]
[Authorize]
[Route(AdvertisementMobileReadEndpointContract.ActiveAdsRoute)]
public sealed class ActiveAdvertisementsController : ControllerBase
{
    private readonly AdvertisementService _service;
    private readonly TimeProvider _clock;

    public ActiveAdvertisementsController(
        AdvertisementService service,
        TimeProvider clock)
    {
        _service = service ??
            throw new ArgumentNullException(nameof(service));

        _clock = clock ??
            throw new ArgumentNullException(nameof(clock));
    }

    // GET /api/mobile/advertisements/active
    // GET /api/mobile/advertisements/active?placement=HomeBanner
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AdvertisementDto>>> GetActive(
        [FromQuery] string? placement,
        CancellationToken cancellationToken)
    {
        // Reject unsupported query parameters.
        foreach (var queryKey in Request.Query.Keys)
        {
            if (!string.Equals(
                    queryKey,
                    AdvertisementMobileReadEndpointContract.PlacementParameter,
                    StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(
                    "Unsupported advertisement query parameter.");
            }
        }

        AdvertisementPlacement? parsedPlacement = null;

        if (placement is not null)
        {
            if (!AdvertisementMobileReadEndpointContract
                .IsSupportedPlacement(placement))
            {
                return BadRequest(
                    "Unsupported advertisement placement.");
            }

            parsedPlacement =
                Enum.Parse<AdvertisementPlacement>(placement);
        }

        // Server time only.
        var nowUtc = _clock.GetUtcNow();

        IReadOnlyList<AdvertisementDto> result;

        if (parsedPlacement.HasValue)
        {
            result = await _service.GetActiveByPlacementAsync(
                nowUtc,
                parsedPlacement.Value,
                cancellationToken);
        }
        else
        {
            result = await _service.GetActiveAsync(
                nowUtc,
                cancellationToken);
        }

        return Ok(result);
    }
}
