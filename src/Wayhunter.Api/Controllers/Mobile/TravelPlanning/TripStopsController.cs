
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
public sealed class TripStopsController : ControllerBase
{
    private readonly AddTripStopService _service;

    public TripStopsController(AddTripStopService service)
    {
        _service = service;
    }

    [HttpPost]
    public async Task<ActionResult<TripPlanResponseDto>> Add(
        Guid tripPlanId,
        [FromBody] AddTripStopDto request,
        CancellationToken cancellationToken)
    {
        var userId = User.Identity?.IsAuthenticated == true
            ? User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue("sub")
            : null;

        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        try
        {
            var result = await _service.AddAsync(
                tripPlanId,
                request,
                userId,
                cancellationToken);

            if (result is null)
                return NotFound();

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
                message = ex.Message
            });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new
            {
                message = ex.Message
            });
        }
    }
}
