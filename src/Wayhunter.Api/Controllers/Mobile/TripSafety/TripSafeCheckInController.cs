using System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Wayhunter.Application.Modules.TripSafety.Interfaces;
using Wayhunter.Application.Modules.TripSafety.Services;
using Wayhunter.Api.Contracts;

namespace Wayhunter.Api.Controllers.Mobile.TripSafety;

public sealed record TripSafeCheckInApiResponse(Guid CheckInId, Guid TripPlanId, Guid SessionId, string UserId, DateTimeOffset CheckedInAtUtc, bool Created);

[ApiController]
[Authorize]
public sealed class TripSafeCheckInController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly IServiceProvider _services;

    public TripSafeCheckInController(
        IConfiguration configuration,
        IServiceProvider services)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _services = services ?? throw new ArgumentNullException(nameof(services));
    }

    [HttpPost("api/mobile/trips/{tripPlanId:guid}/sessions/{sessionId:guid}/im-safe")]
    public async Task<IActionResult> MarkSafe(
        Guid tripPlanId,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        if (!TrustedIdentityAccessContract.TryResolveUserId(User, out _))
        {
            return Unauthorized();
        }

        if (!_configuration.GetValue<bool>("TripSafety:EnableSafeCheckIn") ||
            !_configuration.GetValue<bool>("TripSafety:EnableSqlPersistence"))
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { message = "Safe check-in is unavailable." });
        }

        if (!TrustedIdentityAccessContract.TryResolveUserId(User, out var userId))
            return Unauthorized();

        try
        {
            var service = _services.GetService<TripSafeCheckInService>();
            if (service is null)
                return StatusCode(StatusCodes.Status503ServiceUnavailable,
                    new { message = "Safe check-in is unavailable." });

            var result = await service.MarkSafeAsync(
                tripPlanId, sessionId, userId, cancellationToken);
            return Ok(new TripSafeCheckInApiResponse(
                result.CheckIn.CheckInId,
                result.CheckIn.TripPlanId,
                result.CheckIn.SessionId,
                result.CheckIn.UserId,
                result.CheckIn.CheckedInAtUtc,
                result.Created));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { message = exception.Message });
        }
        catch (TripSafetyCheckInUnavailableException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { message = "Safe check-in is unavailable." });
        }
    }
}
