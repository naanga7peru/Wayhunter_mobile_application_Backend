
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wayhunter.Application.Modules.TravelPlanning.Commands;
using Wayhunter.Application.Modules.TravelPlanning.Dtos;
using Wayhunter.Application.Modules.TravelPlanning.Exceptions;
using Wayhunter.Application.Modules.TravelPlanning.Queries;

namespace Wayhunter.Api.Controllers.Mobile.TravelPlanning;

[ApiController]
[Authorize]
[Route("api/mobile/trip-plans")]
public sealed class TripPlansController : ControllerBase
{
    private readonly CreateTripPlanService _createService;
    private readonly GetTripPlanService _getService;
    private readonly UpdateTripPlanService _updateService;
    private readonly CancelTripPlanService _cancelService;

    public TripPlansController(
        CreateTripPlanService createService,
        GetTripPlanService getService,
        UpdateTripPlanService updateService,
        CancelTripPlanService cancelService)
    {
        _createService = createService;
        _getService = getService;
        _updateService = updateService;
        _cancelService = cancelService;
    }

    private string? GetAuthenticatedUserId()
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        return User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");
    }

    // POST /api/mobile/trip-plans
    [HttpPost]
    public async Task<ActionResult<TripPlanResponseDto>> Create(
        [FromBody] CreateTripPlanDto request,
        CancellationToken cancellationToken)
    {
        var userId = GetAuthenticatedUserId();

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        try
        {
            var result = await _createService.CreateAsync(
                request,
                userId,
                cancellationToken);

            return CreatedAtAction(
                nameof(GetById),
                new { tripPlanId = result.TripPlanId },
                result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }

    // GET /api/mobile/trip-plans
    // Return only the authenticated user's trip plans.
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TripPlanResponseDto>>> GetMyPlans(
        CancellationToken cancellationToken)
    {
        var userId = GetAuthenticatedUserId();

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        try
        {
            var result = await _getService.GetByOwnerAsync(
                userId,
                cancellationToken);

            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }

    // GET /api/mobile/trip-plans/{tripPlanId}
    [HttpGet("{tripPlanId:guid}")]
    public async Task<ActionResult<TripPlanResponseDto>> GetById(
        Guid tripPlanId,
        CancellationToken cancellationToken)
    {
        var userId = GetAuthenticatedUserId();

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        try
        {
            var result = await _getService.GetByIdAsync(
                tripPlanId,
                userId,
                cancellationToken);

            if (result is null)
            {
                return NotFound();
            }

            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }

    // PUT /api/mobile/trip-plans/{tripPlanId}
    [HttpPut("{tripPlanId:guid}")]
    public async Task<ActionResult<TripPlanResponseDto>> Update(
        Guid tripPlanId,
        [FromBody] UpdateTripPlanDto request,
        CancellationToken cancellationToken)
    {
        var userId = GetAuthenticatedUserId();

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        try
        {
            var result = await _updateService.UpdateAsync(
                tripPlanId,
                request,
                userId,
                cancellationToken);

            if (result is null)
            {
                return NotFound();
            }

            return Ok(result);
        }
        catch (TripPlanConcurrencyException ex)
        {
            return Conflict(new
            {
                message = ex.Message,
                code = "TRIP_PLAN_CONCURRENCY_CONFLICT",
                tripPlanId = ex.TripPlanId
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

    // DELETE /api/mobile/trip-plans/{tripPlanId}
    [HttpDelete("{tripPlanId:guid}")]
    public async Task<IActionResult> Cancel(
        Guid tripPlanId,
        CancellationToken cancellationToken)
    {
        var userId = GetAuthenticatedUserId();

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        try
        {
            var result = await _cancelService.CancelAsync(
                tripPlanId,
                userId,
                cancellationToken);

            if (result is null)
            {
                return NotFound();
            }

            if (result == false)
            {
                return Conflict(new
                {
                    message = "Trip is already cancelled."
                });
            }

            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }
}
