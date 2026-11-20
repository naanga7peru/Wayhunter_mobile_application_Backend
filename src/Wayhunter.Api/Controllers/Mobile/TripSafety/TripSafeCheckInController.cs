using System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wayhunter.Api.Contracts;

namespace Wayhunter.Api.Controllers.Mobile.TripSafety;

public sealed record TripSafeCheckInApiResponse(Guid CheckInId, Guid TripPlanId, Guid SessionId, string UserId, DateTimeOffset CheckedInAtUtc, bool Created);

[ApiController]
[Authorize]
public sealed class TripSafeCheckInController : ControllerBase
{
    [HttpPost("api/mobile/trips/{tripPlanId:guid}/sessions/{sessionId:guid}/im-safe")]
    public IActionResult MarkSafe(Guid tripPlanId, Guid sessionId)
    {
        if (!TrustedIdentityAccessContract.TryResolveUserId(User, out _))
        {
            return Unauthorized();
        }

        // Configuration cannot enable this route before durable persistence
        // and notification enqueue are available together.
        return StatusCode(StatusCodes.Status503ServiceUnavailable,
            new { message = "Safe check-in is unavailable." });
    }
}
