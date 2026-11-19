using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wayhunter.Application.Modules.TravelPlanning.Dtos;
using Wayhunter.Application.Modules.TravelPlanning.Queries;

namespace Wayhunter.Api.Controllers.Mobile.TravelPlanning;

[ApiController]
[Authorize]
[Route("api/mobile/trip-plans/suggested")]
public sealed class SuggestedTripPlansController : ControllerBase
{
    private readonly GetSuggestedTripPlansService _service;

    public SuggestedTripPlansController(GetSuggestedTripPlansService service)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
    }

    // GET /api/mobile/trip-plans/suggested?limit=20
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SuggestedTripPlanDto>>> Get(
        [FromQuery] int limit = GetSuggestedTripPlansService.DefaultLimit,
        CancellationToken cancellationToken = default)
    {
        var userId = User.Identity?.IsAuthenticated == true
            ? User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue("sub")
            : null;

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        try
        {
            var result = await _service.GetAsync(
                userId, limit, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return BadRequest(new
            {
                code = "INVALID_SUGGESTED_PLAN_LIMIT",
                message = ex.Message
            });
        }
    }
}
