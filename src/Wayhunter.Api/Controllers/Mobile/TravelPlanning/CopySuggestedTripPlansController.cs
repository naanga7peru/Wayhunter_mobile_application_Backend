using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wayhunter.Application.Modules.TravelPlanning.Commands;
using Wayhunter.Application.Modules.TravelPlanning.Dtos;

namespace Wayhunter.Api.Controllers.Mobile.TravelPlanning;

[ApiController]
[Authorize]
[Route("api/mobile/trip-plans/suggested")]
public sealed class CopySuggestedTripPlansController : ControllerBase
{
    private readonly CopySuggestedTripPlanService _service;

    public CopySuggestedTripPlansController(CopySuggestedTripPlanService service)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
    }

    // POST /api/mobile/trip-plans/suggested/{sourceTripPlanId}/copy
    [HttpPost("{sourceTripPlanId:guid}/copy")]
    public async Task<ActionResult<CopiedSuggestedTripPlanDto>> Copy(
        Guid sourceTripPlanId,
        CancellationToken cancellationToken)
    {
        var userId = User.Identity?.IsAuthenticated == true
            ? User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")
            : null;
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();
        if (sourceTripPlanId == Guid.Empty)
            return BadRequest(new { code = "INVALID_SOURCE_TRIP_ID" });

        var copied = await _service.CopyAsync(sourceTripPlanId, userId, cancellationToken);
        if (copied is null) return NotFound(); // No existence/permission oracle.

        return CreatedAtAction(
            nameof(TripPlansController.GetById),
            "TripPlans",
            new { tripPlanId = copied.TripPlanId },
            copied);
    }
}
