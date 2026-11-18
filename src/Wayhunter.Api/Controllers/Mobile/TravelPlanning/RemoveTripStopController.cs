
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
public sealed class RemoveTripStopController : ControllerBase
{
    private readonly RemoveTripStopService _service;

    public RemoveTripStopController(RemoveTripStopService service)
    {
        _service = service;
    }

    [HttpDelete("{stopId:guid}")]
    public async Task<ActionResult<TripPlanResponseDto>> Remove(
        Guid tripPlanId,
        Guid stopId,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken cancellationToken)
    {
        var userId = User.Identity?.IsAuthenticated == true
            ? User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue("sub")
            : null;

        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        byte[]? expectedRowVersion = null;

        if (!string.IsNullOrWhiteSpace(ifMatch))
        {
            var token = ifMatch.Trim();

            if (token.StartsWith(
                "W/", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new
                {
                    message = "Weak ETags are not supported."
                });
            }

            if (token.StartsWith("\"") &&
                token.EndsWith("\"") &&
                token.Length >= 2)
            {
                token = token[1..^1];
            }

            try
            {
                expectedRowVersion =
                    Convert.FromBase64String(token);
            }
            catch (FormatException)
            {
                return BadRequest(new
                {
                    message = "Invalid If-Match RowVersion."
                });
            }
        }

        try
        {
            var result = await _service.RemoveAsync(
                tripPlanId,
                stopId,
                new RemoveTripStopDto(expectedRowVersion),
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
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }
}
