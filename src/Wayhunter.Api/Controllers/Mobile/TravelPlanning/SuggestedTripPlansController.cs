using System;
using System.Collections.Generic;
using Wayhunter.Api.Contracts;
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
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class SuggestedTripPlansController : ControllerBase
{
    private readonly GetSuggestedTripPlansService _service;

    public SuggestedTripPlansController(GetSuggestedTripPlansService service)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
    }

    [HttpGet("{tripPlanId:guid}")]
    public async Task<ActionResult<SuggestedTripPlanDto>> GetById(
        Guid tripPlanId, CancellationToken cancellationToken)
    {
        if (!TrustedIdentityAccessContract.TryResolveUserId(User, out var userId))
            return Unauthorized();

        if (tripPlanId == Guid.Empty)
            return BadRequest(new { code = "INVALID_SOURCE_TRIP_ID" });

        var result = await _service.GetByIdAsync(userId, tripPlanId, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    // GET /api/mobile/trip-plans/suggested?limit=20
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SuggestedTripPlanDto>>> Get(
        [FromQuery] int limit = GetSuggestedTripPlansService.DefaultLimit,
        CancellationToken cancellationToken = default)
    {
        if (!TrustedIdentityAccessContract.TryResolveUserId(User, out var userId))
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
