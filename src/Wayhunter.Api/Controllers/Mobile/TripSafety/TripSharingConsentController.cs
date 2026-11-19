
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using Wayhunter.Api.Contracts;

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
            ?? throw new ArgumentNullException(
                nameof(service));
    }

    // PUT /api/mobile/trips/{tripPlanId}/
    //     sessions/{sessionId}/gps-consent
    //
    // Request:
    // {
    //     "isGranted": true
    // }
    //
    // The authenticated user's own consent only.
    // Never trust a UserId supplied by the client.

    [HttpPut("{sessionId:guid}/gps-consent")]
    public async Task<ActionResult<GpsConsentDto>> SetMine(
        Guid tripPlanId,
        Guid sessionId,
        [FromBody] SetMyTripGpsConsentRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // STEP 1: Resolve trusted M1 identity.
        //
        // This rejects:
        // - Anonymous principal
        // - Missing identity claims
        // - Duplicate identity claims
        // - Conflicting NameIdentifier/sub claims
        // - Invalid GUID identity
        // - Empty GUID identity
        //
        // Do not use FindFirstValue fallback here.

        if (!TrustedIdentityAccessContract.TryResolveUserId(
                User,
                out var authenticatedUserId))
        {
            return Unauthorized();
        }

        // STEP 2: Validate request.

        if (request is null)
        {
            return BadRequest(new
            {
                message = "Consent request is required."
            });
        }

        try
        {
            // STEP 3: Update only this user's consent.
            //
            // Existing service and repository remain
            // responsible for:
            // - Valid trip and session
            // - Active accepted membership
            // - Session-state restrictions
            // - Explicit GPS consent
            // - Revocation rules
            //
            // A valid identity alone does not grant
            // permission to change GPS consent.

            var result = await _service.SetMyConsentAsync(
                tripPlanId,
                sessionId,
                authenticatedUserId,
                request.IsGranted,
                cancellationToken);

            return Ok(result);
        }
        catch (UnauthorizedAccessException)
        {
            // Authenticated but not authorized
            // for the requested trip/session.

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
            // Do not expose details of
            // non-existent resources.

            return NotFound();
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new
            {
                message = exception.Message
            });
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new
            {
                message = exception.Message
            });
        }
    }
}
