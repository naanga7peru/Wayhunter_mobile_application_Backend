using System;
using Wayhunter.Api.Contracts;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wayhunter.Application.Modules.TravelPlanning.Commands;
using Wayhunter.Application.Modules.TravelPlanning.Dtos;
using Wayhunter.Application.Modules.TravelPlanning.Interfaces;
using System.ComponentModel;

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
    [EndpointDescription("Requires an Idempotency-Key of 1–128 ASCII letters, digits, dots, underscores, or hyphens. An eligible retry returns the existing draft with HTTP 201. Reusing the key for a different source returns HTTP 409.")]
    [ProducesResponseType<CopiedSuggestedTripPlanDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CopiedSuggestedTripPlanDto>> Copy(
        Guid sourceTripPlanId,
        [FromHeader(Name = "Idempotency-Key")]
        [Description("Required. Reuse the same key for the same source to recover the existing draft. A different source with the same key returns 409.")]
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (!TrustedIdentityAccessContract.TryResolveUserId(User, out var userId))
            return Unauthorized();
        if (sourceTripPlanId == Guid.Empty)
            return BadRequest(new { code = "INVALID_SOURCE_TRIP_ID" });
        if (Request.Headers["Idempotency-Key"].Count != 1)
            return BadRequest(new { code = "INVALID_IDEMPOTENCY_KEY" });

        CopiedSuggestedTripPlanDto? copied;
        try
        {
            copied = await _service.CopyAsync(sourceTripPlanId, userId, idempotencyKey ?? "", cancellationToken);
        }
        catch (ArgumentException ex) when (ex.ParamName == "idempotencyKey")
        {
            return BadRequest(new { code = "INVALID_IDEMPOTENCY_KEY" });
        }
        catch (CopyRequestConflictException)
        {
            return Conflict(new { code = "COPY_REQUEST_CONFLICT" });
        }
        if (copied is null) return NotFound(); // No existence/permission oracle.

        return CreatedAtAction(
            nameof(TripPlansController.GetById),
            "TripPlans",
            new { tripPlanId = copied.TripPlanId },
            copied);
    }

    [HttpPut("/api/mobile/trip-plans/{tripPlanId:guid}/reuse-consent")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetReuseConsent(Guid tripPlanId,
        ReuseConsentRequest request, CancellationToken cancellationToken)
    {
        if (!TrustedIdentityAccessContract.TryResolveUserId(User, out var userId))
            return Unauthorized();
        if (tripPlanId == Guid.Empty || request.Allowed is not bool allowed)
            return BadRequest(new { code = "INVALID_REUSE_CONSENT" });
        return await _service.SetReuseConsentAsync(tripPlanId, userId, allowed, cancellationToken)
            ? NoContent()
            : NotFound();
    }
}

public sealed record ReuseConsentRequest(bool? Allowed);
