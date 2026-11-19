
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using Wayhunter.Api.Contracts;

using Wayhunter.Application.Modules.TripSafety.Interfaces;
using Wayhunter.Application.Modules.TripSafety.Services;

using Wayhunter.Domain.Modules.TripSafety.Entities;
using Wayhunter.Domain.Modules.TripSafety.Enums;

namespace Wayhunter.Api.Controllers.Mobile.TripSafety;

public sealed record CreateTripInvitationRequest(
    string RecipientUserId);

public sealed record TripInvitationApiResponse(
    Guid InvitationId,
    Guid TripPlanId,
    string SenderUserId,
    string RecipientUserId,
    TripInvitationStatus Status,
    DateTimeOffset CreatedAtUtc);

[ApiController]
[Authorize]
public sealed class TripInvitationCreateController
    : ControllerBase
{
    private readonly TripInvitationCreateService _service;

    public TripInvitationCreateController(
        TripInvitationCreateService service)
    {
        _service = service
            ?? throw new ArgumentNullException(nameof(service));
    }

    // POST /api/mobile/trips/{tripPlanId}/invitations
    //
    // Request:
    // {
    //   "recipientUserId": "<GUID>"
    // }
    //
    // The server determines the sender from
    // authenticated M1 identity claims.

    [HttpPost(
        "api/mobile/trips/{tripPlanId:guid}/invitations")]
    public async Task<IActionResult> Create(
        Guid tripPlanId,
        [FromBody] CreateTripInvitationRequest request,
        CancellationToken cancellationToken)
    {
        if (!TrustedIdentityAccessContract.TryResolveUserId(
                User,
                out var sender))
        {
            return Unauthorized();
        }

        if (request is null ||
            string.IsNullOrWhiteSpace(request.RecipientUserId))
        {
            return BadRequest(new
            {
                message = "Recipient user ID is required."
            });
        }

        try
        {
            var invitation = await _service.CreateAsync(
                tripPlanId,
                sender,
                request.RecipientUserId,
                cancellationToken);

            // HTTP 201 = invitation persisted.
            // Push delivery is a separate notification task.

            return StatusCode(
                StatusCodes.Status201Created,
                ToResponse(invitation));
        }
        catch (TripInvitePrerequisiteUnavailableException)
        {
            // No M1 blocked relationship reader.
            // Never create an invitation without
            // authoritative eligibility verification.

            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new
                {
                    message =
                        "M1 invitation eligibility integration is not configured."
                });
        }
        catch (UnauthorizedAccessException)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden);
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

    // GET /api/mobile/trip-invitations/pending
    //
    // Recipient can only see their own inbox.

    [HttpGet(
        "api/mobile/trip-invitations/pending")]
    public async Task<IActionResult> MyPending(
        CancellationToken cancellationToken)
    {
        if (!TrustedIdentityAccessContract.TryResolveUserId(
                User,
                out var recipient))
        {
            return Unauthorized();
        }

        try
        {
            var pending = await _service.GetMyPendingAsync(
                recipient,
                cancellationToken);

            return Ok(
                pending.Select(ToResponse).ToArray());
        }
        catch (TripInvitePrerequisiteUnavailableException)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable);
        }
    }

    private static TripInvitationApiResponse ToResponse(
        TripInvitation invitation)
    {
        return new TripInvitationApiResponse(
            invitation.Id,
            invitation.TripPlanId,
            invitation.SenderUserId,
            invitation.RecipientUserId,
            invitation.Status,
            invitation.CreatedAtUtc);
    }
}
