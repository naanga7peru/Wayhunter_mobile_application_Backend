
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using Wayhunter.Api.Contracts;
using Wayhunter.Application.Modules.TripSafety.Services;

namespace Wayhunter.Api.Controllers.Mobile.TripSafety;

public sealed record ResolveTripHazardRequest(
    string Reason);

[ApiController]
[Authorize]
public sealed class TripHazardsController : ControllerBase
{
    private readonly TripHazardService _service;

    public TripHazardsController(
        TripHazardService service)
    {
        _service = service ??
            throw new ArgumentNullException(nameof(service));
    }

    private bool TryAuthenticatedUser(out string userId)
    {
        return TrustedIdentityAccessContract.TryResolveUserId(
            User,
            out userId);
    }

    private static bool IsExpected(Exception exception)
    {
        return exception is ArgumentException
            or KeyNotFoundException
            or UnauthorizedAccessException
            or InvalidOperationException;
    }

    private static IActionResult ToError(Exception exception)
    {
        return exception switch
        {
            UnauthorizedAccessException =>
                new StatusCodeResult(
                    StatusCodes.Status403Forbidden),

            KeyNotFoundException =>
                new NotFoundResult(),

            ArgumentException error =>
                new BadRequestObjectResult(new
                {
                    message = error.Message
                }),

            InvalidOperationException error =>
                new ConflictObjectResult(new
                {
                    message = error.Message
                }),

            _ => throw exception
        };
    }

    // =====================================================
    // MOBILE: REPORT
    // =====================================================

    // POST /api/mobile/trips/{tripPlanId}/hazards
    //
    // Explicit report from owner or active trip member.
    // Initial status is always Reported / Unverified.

    [HttpPost("api/mobile/trips/{tripPlanId:guid}/hazards")]
    public async Task<IActionResult> Report(
        Guid tripPlanId,
        [FromBody] CreateTripHazardRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryAuthenticatedUser(out var userId))
        {
            return Unauthorized();
        }

        if (request is null)
        {
            return BadRequest(new
            {
                message = "Hazard report body is required."
            });
        }

        try
        {
            var result = await _service.ReportAsync(
                tripPlanId,
                userId,
                request,
                cancellationToken);

            return StatusCode(
                StatusCodes.Status201Created,
                result);
        }
        catch (Exception exception)
            when (IsExpected(exception))
        {
            return ToError(exception);
        }
    }

    // =====================================================
    // MOBILE: ROUTE-SCOPED WARNING QUERY
    // =====================================================

    // GET /api/mobile/trips/{tripPlanId}/hazards
    //
    // Only active/eligible route warnings.
    // Never broadcast all worldwide hazard records.

    [HttpGet("api/mobile/trips/{tripPlanId:guid}/hazards")]
    public async Task<IActionResult> GetWarnings(
        Guid tripPlanId,
        CancellationToken cancellationToken)
    {
        if (!TryAuthenticatedUser(out var userId))
        {
            return Unauthorized();
        }

        try
        {
            var result =
                await _service.GetWarningsForTripAsync(
                    tripPlanId,
                    userId,
                    cancellationToken);

            return Ok(result);
        }
        catch (Exception exception)
            when (IsExpected(exception))
        {
            return ToError(exception);
        }
    }

    // =====================================================
    // ADMIN: VERIFY
    // =====================================================

    // POST /api/admin/hazards/{hazardId}/verify
    //
    // Role must be issued by the trusted M1
    // authentication system, not request JSON.

    [Authorize(Roles = "Admin")]
    [HttpPost("api/admin/hazards/{hazardId:guid}/verify")]
    public async Task<IActionResult> Verify(
        Guid hazardId,
        CancellationToken cancellationToken)
    {
        if (!TryAuthenticatedUser(out var userId))
        {
            return Unauthorized();
        }

        if (!User.IsInRole("Admin"))
        {
            return StatusCode(
                StatusCodes.Status403Forbidden);
        }

        try
        {
            var result = await _service.VerifyAsync(
                hazardId,
                userId,
                cancellationToken);

            return Ok(result);
        }
        catch (Exception exception)
            when (IsExpected(exception))
        {
            return ToError(exception);
        }
    }

    // =====================================================
    // ADMIN: RESOLVE
    // =====================================================

    // POST /api/admin/hazards/{hazardId}/resolve

    [Authorize(Roles = "Admin")]
    [HttpPost("api/admin/hazards/{hazardId:guid}/resolve")]
    public async Task<IActionResult> Resolve(
        Guid hazardId,
        [FromBody] ResolveTripHazardRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryAuthenticatedUser(out var userId))
        {
            return Unauthorized();
        }

        if (!User.IsInRole("Admin"))
        {
            return StatusCode(
                StatusCodes.Status403Forbidden);
        }

        if (request is null)
        {
            return BadRequest(new
            {
                message = "Resolution reason is required."
            });
        }

        try
        {
            var result = await _service.ResolveAsync(
                hazardId,
                userId,
                request.Reason,
                cancellationToken);

            return Ok(result);
        }
        catch (Exception exception)
            when (IsExpected(exception))
        {
            return ToError(exception);
        }
    }
}
