using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wayhunter.Api.Contracts;
using Wayhunter.Application.Modules.HunterPlaces.Services;
using Wayhunter.Api.Services.HunterPlaces;
using Wayhunter.Application.Modules.HunterPlaces.Commands;
using Wayhunter.Application.Modules.HunterPlaces.Dtos;
using Wayhunter.Application.Modules.HunterPlaces.Interfaces;
using Wayhunter.Domain.Modules.HunterPlaces.Enums;

namespace Wayhunter.IntegrationTests.Modules.HunterPlaces;

// This application part is loaded only by the candidate test host.
[ApiController]
[Authorize]
[Route("api/mobile/v1/hunter")]
public sealed class HunterContractCandidateController : ControllerBase
{
    [HttpPost("drafts")]
    public async Task<IActionResult> CreateDraft(
        DraftCreate request, [FromServices] HunterVerifiedDraftCandidate candidate, CancellationToken ct)
    {
        if (!TrustedIdentityAccessContract.TryResolveUserId(User, out var userId))
            return Unauthorized();

        try
        {
            var result = await candidate.CreateAsync(userId, request.PlaceName, request.Description,
                new HunterLocationSample(request.Latitude, request.Longitude, request.AccuracyMeters,
                    request.CapturedAtUtc), ct);
            return result.Outcome switch
            {
                HunterLocationOutcome.Verified => StatusCode(201, result.Draft),
                HunterLocationOutcome.OutsideCoverage or HunterLocationOutcome.Ineligible => BadRequest(),
                _ => StatusCode(503)
            };
        }
        catch (ArgumentException)
        {
            return BadRequest();
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized();
        }
    }

    public sealed record DraftCreate(string PlaceName, string? Description, double Latitude,
        double Longitude, double AccuracyMeters, DateTimeOffset CapturedAtUtc);

    [HttpGet("hunts/{id:guid}")]
    public async Task<IActionResult> Get(
        Guid id, [FromServices] IHunterOwnedSubmissionReader reader, CancellationToken ct)
    {
        if (!TrustedIdentityAccessContract.TryResolveUserId(User, out var userId))
            return Unauthorized();

        var submission = await reader.GetMySubmissionAsync(userId, id, ct);
        return submission is null ? NotFound() : Ok(submission);
    }

    [HttpGet("drafts/{id:guid}")]
    public async Task<IActionResult> GetDraft(
        Guid id, [FromServices] IHunterOwnedSubmissionReader reader, CancellationToken ct)
    {
        if (!TrustedIdentityAccessContract.TryResolveUserId(User, out var userId))
            return Unauthorized();

        var submission = await reader.GetMySubmissionAsync(userId, id, ct);
        return submission?.Status == HunterSubmissionStatus.Draft ? Ok(submission) : NotFound();
    }

    [HttpGet("hunts")]
    public async Task<IActionResult> List(
        [FromServices] IHunterOwnedSubmissionReader reader, CancellationToken ct)
    {
        if (!TrustedIdentityAccessContract.TryResolveUserId(User, out var userId))
            return Unauthorized();

        return Ok(await reader.ListMySubmissionsAsync(userId, ct));
    }

    [HttpGet("hunts/statistics")]
    public async Task<IActionResult> Statistics(
        [FromServices] IHunterOwnedSubmissionReader reader, CancellationToken ct)
    {
        if (!TrustedIdentityAccessContract.TryResolveUserId(User, out var userId))
            return Unauthorized();

        return Ok(new { approvedHuntCount = await reader.CountMyApprovedHuntsAsync(userId, ct) });
    }

    [HttpPut("drafts/{id:guid}/details")]
    public async Task<IActionResult> Update(
        Guid id, DraftDetails request, [FromServices] HunterDraftCreationService drafts, CancellationToken ct)
    {
        if (!TrustedIdentityAccessContract.TryResolveUserId(User, out var userId))
            return Unauthorized();

        try
        {
            return await drafts.UpdateDetailsAsync(userId, id, request.PlaceName, request.Description, ct)
                ? NoContent() : NotFound();
        }
        catch (ArgumentException)
        {
            return BadRequest();
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized();
        }
    }

    [HttpPost("drafts/{id:guid}/capture-sessions")]
    public async Task<IActionResult> StartCapture(
        Guid id, HunterStartCaptureSessionRequestDto request,
        [FromServices] HunterStartAuthorizedCaptureSessionHandler handler, CancellationToken ct)
    {
        if (!TrustedIdentityAccessContract.TryResolveUserId(User, out var userId))
            return Unauthorized();

        if (id == Guid.Empty || (request.HunterSubmissionId != Guid.Empty && request.HunterSubmissionId != id))
            return BadRequest();

        request.HunterSubmissionId = id;
        try
        {
            return StatusCode(201, await handler.HandleAsync(userId, request, ct));
        }
        catch (UnauthorizedAccessException)
        {
            return NotFound();
        }
    }

    public sealed record DraftDetails(string PlaceName, string? Description);

    [HttpPost("drafts/{id:guid}/submit")]
    public async Task<IActionResult> Submit(Guid id, HunterSubmitRequestDto request,
        [FromHeader(Name = "Idempotency-Key")] string? operationKey,
        [FromServices] HunterSubmitWorkflowCandidate workflow, CancellationToken ct)
    {
        if (!TrustedIdentityAccessContract.TryResolveUserId(User, out var userId))
            return Unauthorized();

        try
        {
            var result = await workflow.SubmitAsync(userId, id, operationKey ?? "", request, ct);
            return result is null ? Conflict() : Ok(result);
        }
        catch (UnauthorizedAccessException)
        {
            return NotFound();
        }
        catch (ArgumentException)
        {
            return BadRequest();
        }
        catch (InvalidOperationException)
        {
            return Conflict();
        }
    }
}
