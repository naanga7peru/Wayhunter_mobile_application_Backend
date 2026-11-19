
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using Wayhunter.Api.Contracts;
using Wayhunter.Api.Hubs.TripSafety;

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
    private readonly TripLocationSubscriptionCleanup? _cleanup;

    public TripSharingConsentController(
        TripSharingConsentService service,
        TripLocationSubscriptionCleanup? cleanup = null)
    {
        _service = service
            ?? throw new ArgumentNullException(
                nameof(service));

        _cleanup = cleanup;
    }

    [HttpPut("{sessionId:guid}/gps-consent")]
    public async Task<ActionResult<GpsConsentDto>> SetMine(
        Guid tripPlanId,
        Guid sessionId,
        [FromBody] SetMyTripGpsConsentRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Always validate M1 trusted identity first.
        if (!TrustedIdentityAccessContract.TryResolveUserId(
                User,
                out var authenticatedUserId))
        {
            return Unauthorized();
        }

        if (request is null)
        {
            return BadRequest(new
            {
                message = "Consent request is required."
            });
        }

        try
        {
            // Repository validates:
            // - Active accepted membership
            // - Correct session/trip
            // - Session state
            // - Per-person explicit GPS consent

            var result = await _service.SetMyConsentAsync(
                tripPlanId,
                sessionId,
                authenticatedUserId,
                request.IsGranted,
                cancellationToken);

            // Successful consent revocation:
            // immediately remove local subscriptions.
            //
            // Database authorization remains
            // authoritative even during cleanup.

            if (!request.IsGranted &&
                _cleanup is not null)
            {
                await _cleanup.RevokeMemberFromSessionAsync(
                    tripPlanId,
                    sessionId,
                    authenticatedUserId,
                    CancellationToken.None);
            }

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
