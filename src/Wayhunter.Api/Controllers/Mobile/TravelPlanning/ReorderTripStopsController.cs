
using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wayhunter.Application.Modules.TravelPlanning.Commands;
using Wayhunter.Application.Modules.TravelPlanning.Dtos;
using Wayhunter.Application.Modules.TravelPlanning.Exceptions;

namespace Wayhunter.Api.Controllers.Mobile.TravelPlanning;

[ApiController]
[Authorize]
[Route("api/mobile/trip-plans/{tripPlanId:guid}/stops")]
public sealed class ReorderTripStopsController : ControllerBase
{
    private readonly ReorderTripStopsService _service;

    public ReorderTripStopsController(
        ReorderTripStopsService service)
    {
        _service = service
            ?? throw new ArgumentNullException(nameof(service));
    }

    // ==========================================
    // M2-MS04 - REORDER TRIP STOPS
    // ==========================================
    // PUT /api/mobile/trip-plans/{tripPlanId}/stops/order

    [HttpPut("order")]
    public async Task<ActionResult<TripPlanResponseDto>> Reorder(
        Guid tripPlanId,
        [FromBody] ReorderTripStopsDto request,
        CancellationToken cancellationToken)
    {
        // STEP 1 - Get authenticated User ID.
        var userId = User.Identity?.IsAuthenticated == true
            ? User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue("sub")
            : null;

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        // STEP 2 - Validate request.
        if (request is null || request.StopIds is null)
        {
            return BadRequest(new
            {
                code = "INVALID_STOP_ORDER",
                message = "Stop IDs are required."
            });
        }

        try
        {
            // STEP 3 - Service validates ownership,
            // Stop IDs, draft status and RowVersion.
            var result = await _service.ReorderAsync(
                tripPlanId,
                request,
                userId,
                cancellationToken);

            // STEP 4 - Trip does not exist
            // or belongs to another user.
            if (result is null)
            {
                return NotFound(new
                {
                    code = "TRIP_PLAN_NOT_FOUND",
                    message = "Trip plan not found."
                });
            }

            // STEP 5 - Return latest ordered stops.
            return Ok(result);
        }
        catch (TripPlanConcurrencyException ex)
        {
            return Conflict(new
            {
                code = "TRIP_PLAN_CONCURRENCY_CONFLICT",
                message = ex.Message
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new
            {
                code = "INVALID_STOP_ORDER",
                message = ex.Message
            });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new
            {
                code = "TRIP_PLAN_NOT_EDITABLE",
                message = ex.Message
            });
        }
    }
}
