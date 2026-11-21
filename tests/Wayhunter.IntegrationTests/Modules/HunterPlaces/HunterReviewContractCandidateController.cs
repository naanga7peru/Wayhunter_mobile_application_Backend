using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wayhunter.Api.Contracts;
using Wayhunter.Application.Modules.HunterPlaces.Dtos;
using Wayhunter.Application.Modules.HunterPlaces.Interfaces;
using Wayhunter.Application.Modules.HunterPlaces.Services;
using Wayhunter.Application.Modules.HunterPlaces.Validation;
using Wayhunter.Domain.Modules.HunterPlaces.Enums;
using Wayhunter.Infrastructure.Persistence.Repositories.HunterPlaces;

namespace Wayhunter.IntegrationTests.Modules.HunterPlaces;

[ApiController]
[Authorize(Policy = Policy)]
[Route("api/admin/v1/hunter/reviews")]
public sealed class HunterReviewContractCandidateController : ControllerBase
{
    public const string Policy = "HunterReviewCandidate.TestOnly";

    [HttpGet]
    public async Task<IActionResult> Queue(
        [FromServices] SqlHunterCaptureSubmissionAccessReader reader,
        CancellationToken ct, int offset = 0, int limit = 20)
    {
        if (!TrustedIdentityAccessContract.TryResolveUserId(User, out _))
            return Unauthorized();

        if (offset < 0 || limit is < 1 or > 100)
            return BadRequest();

        return Ok(await reader.ListPendingReviewCandidatesAsync(offset, limit, ct));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Detail(
        Guid id, [FromServices] SqlHunterCaptureSubmissionAccessReader reader, CancellationToken ct)
    {
        if (!TrustedIdentityAccessContract.TryResolveUserId(User, out _))
            return Unauthorized();

        var submission = await reader.FindPendingReviewCandidateAsync(id, ct);
        return submission is null ? NotFound() : Ok(submission);
    }

    [HttpGet("{id:guid}/evidence/{evidenceId:guid}")]
    public async Task<IActionResult> Evidence(Guid id, Guid evidenceId,
        [FromServices] IHunterPrivateEvidenceCandidate evidence, CancellationToken ct)
    {
        if (!TrustedIdentityAccessContract.TryResolveUserId(User, out var reviewer))
            return Unauthorized();

        if (id == Guid.Empty || evidenceId == Guid.Empty)
            return BadRequest();

        var access = await evidence.AuthorizeAsync(reviewer, id, evidenceId, ct);
        if (access is null)
            return NotFound();

        Response.Headers.CacheControl = "no-store";
        return Ok(access);
    }

    [HttpPost("{id:guid}/decisions")]
    public async Task<IActionResult> Decide(Guid id, DecisionRequest request,
        [FromServices] IHunterReviewService review, CancellationToken ct)
    {
        if (!TrustedIdentityAccessContract.TryResolveUserId(User, out var reviewer))
            return Unauthorized();

        var status = request.Decision?.Trim().ToLowerInvariant() switch
        {
            "approve" => HunterSubmissionStatus.Approved,
            "reject" => HunterSubmissionStatus.Rejected,
            "hold" => HunterSubmissionStatus.Held,
            _ => HunterSubmissionStatus.Draft
        };
        var command = new HunterReviewRequestDto
        {
            HunterSubmissionId = id,
            ResultingStatus = status,
            Reason = request.Reason?.Trim()
        };
        if (HunterReviewRequestValidator.Validate(command).Count != 0)
            return BadRequest();

        try
        {
            return Ok(await review.ReviewAsync(reviewer, command, ct));
        }
        catch (InvalidOperationException)
        {
            return Conflict();
        }
    }

    public sealed record DecisionRequest(string? Decision, string? Reason);
}
