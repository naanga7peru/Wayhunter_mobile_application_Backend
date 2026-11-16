
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wayhunter.Application.Modules.TripSafety.Interfaces;
using Wayhunter.Domain.Modules.TripSafety.Entities;

namespace Wayhunter.Api.Controllers.Mobile.TripSafety;

// DEMO ONLY: These endpoints are intentionally disabled
// until M1 authentication and trip ownership checks
// are integrated.
//
// Do not remove NonAction/authorization safeguards
// merely to expose these endpoints.

[ApiController]
[Route("api/mobile/trip-safety/sessions")]
public sealed class LiveTripSessionController : ControllerBase
{
    private readonly ILiveTripSessionService _service;

    public LiveTripSessionController(
        ILiveTripSessionService service)
    {
        _service = service;
    }

    [HttpPost("start")]
    [NonAction]
    public async Task<ActionResult<LiveTripSessionResponse>> Start(
        [FromBody] StartLiveTripSessionRequest request,
        CancellationToken cancellationToken)
    {
        if (request.TripPlanId == Guid.Empty)
        {
            return BadRequest("Valid Trip Plan ID is required.");
        }

        try
        {
            var session = await _service.StartAsync(
                request.TripPlanId,
                cancellationToken);

            return Ok(ToResponse(session));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
    }

    [HttpPost("{sessionId:guid}/pause")]
    [NonAction]
    public async Task<ActionResult<LiveTripSessionResponse>> Pause(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        return await ChangeState(
            () => _service.PauseAsync(sessionId, cancellationToken));
    }

    [HttpPost("{sessionId:guid}/resume")]
    [NonAction]
    public async Task<ActionResult<LiveTripSessionResponse>> Resume(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        return await ChangeState(
            () => _service.ResumeAsync(sessionId, cancellationToken));
    }

    [HttpPost("{sessionId:guid}/finish")]
    [NonAction]
    public async Task<ActionResult<LiveTripSessionResponse>> Finish(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        return await ChangeState(
            () => _service.FinishAsync(sessionId, cancellationToken));
    }

    private async Task<ActionResult<LiveTripSessionResponse>> ChangeState(
        Func<Task<LiveTripSession>> operation)
    {
        try
        {
            var session = await operation();
            return Ok(ToResponse(session));
        }
        catch (KeyNotFoundException)
        {
            return NotFound("Live trip session was not found.");
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
    }

    private static LiveTripSessionResponse ToResponse(
        LiveTripSession session)
    {
        return new LiveTripSessionResponse(
            session.SessionId,
            session.TripPlanId,
            session.Status.ToString(),
            session.StartedAtUtc,
            session.FinishedAtUtc);
    }
}

public sealed record StartLiveTripSessionRequest(
    Guid TripPlanId);

public sealed record LiveTripSessionResponse(
    Guid SessionId,
    Guid TripPlanId,
    string Status,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? FinishedAtUtc);
