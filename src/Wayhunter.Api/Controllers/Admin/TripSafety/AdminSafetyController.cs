using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wayhunter.Api.Contracts;
using Wayhunter.Api.Extensions;
using Wayhunter.Application.Modules.TripSafety.Services;

namespace Wayhunter.Api.Controllers.Admin.TripSafety;

[ApiController]
[Authorize(Policy = AdminSafetyServiceCollectionExtensions.Policy)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Route("api/admin/safety/trips/{tripPlanId:guid}/sessions/{sessionId:guid}")]
public sealed class AdminSafetyController(AdminSafetyMonitoringService monitoring) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Read(Guid tripPlanId, Guid sessionId, CancellationToken ct)
    {
        if (!TrustedIdentityAccessContract.TryResolveUserId(User, out var actor))
            return Unauthorized();

        var result = await monitoring.ReadAsync(actor, new(tripPlanId, sessionId), ct);
        return result.Outcome switch
        {
            AdminSafetyOutcome.Success => Ok(result.Summary),
            AdminSafetyOutcome.InvalidTarget => BadRequest(),
            AdminSafetyOutcome.Denied => Forbid(),
            AdminSafetyOutcome.NotFound => NotFound(),
            _ => StatusCode(StatusCodes.Status503ServiceUnavailable)
        };
    }
}
