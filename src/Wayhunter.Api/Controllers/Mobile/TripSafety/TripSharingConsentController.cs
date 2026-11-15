
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wayhunter.Application.Modules.TripSafety.Dtos;
using Wayhunter.Application.Modules.TripSafety.Services;

namespace Wayhunter.Api.Controllers.Mobile.TripSafety;

public sealed record SetMyTripGpsConsentRequest(
    bool IsGranted);

[ApiController]
[Authorize]
[Route("api/mobile/trips/{tripPlanId:guid}/sessions")]
public sealed class TripSharingConsentController
    : ControllerBase
{
    private readonly TripSharingConsentService _service;

    public TripSharingConsentController(
        TripSharingConsentService service)
    {
        _service = service
            ?? throw new ArgumentNullException(nameof(service));
    }

    // PUT /api/mobile/trips/{tripPlanId}/
    //     sessions/{sessionId}/gps-consent
    //
    // Request:
    // {
    //     "isGranted": true
    // }
    //
    // false = revoke sharing.
    //
    // The caller's User ID is taken exclusively
    // from trusted authenticated claims.

    [HttpPut("{sessionId:guid}/gps-consent")]
    public async Task<ActionResult<GpsConsentDto>> SetMine(
        Guid tripPlanId,
        Guid sessionId,
        [FromBody] SetMyTripGpsConsentRequest request,
        CancellationToken cancellationToken)
    {
        // 1. Require authentication.
        if (User.Identity?.IsAuthenticated != true)
        {
            return Unauthorized();
        }

        // 2. Obtain trusted User ID.
        var userId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        // 3. Validate request body.
        if (request is null)
        {
            return BadRequest(new
            {
                message = "Consent request is required."
            });
        }

        try
        {
            // 4. Change only the caller's own consent.
            var result =
                await _service.SetMyConsentAsync(
                    tripPlanId,
                    sessionId,
                    userId,
                    request.IsGranted,
                    cancellationToken);

            return Ok(result);
        }
        catch (UnauthorizedAccessException)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new
                {
                    message =
                        "User is not authorized to change this GPS consent."
                });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
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
