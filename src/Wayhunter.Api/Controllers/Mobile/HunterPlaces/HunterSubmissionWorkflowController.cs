using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wayhunter.Api.Authorization;
using Wayhunter.Api.Contracts;
using Wayhunter.Api.Contracts.Responses;
using Wayhunter.Api.Services.IdentitySocial;
using Wayhunter.Application.Modules.HunterPlaces.Dtos;
using Wayhunter.Application.Modules.HunterPlaces.Interfaces;
using Wayhunter.Application.Modules.HunterPlaces.Services;
using Wayhunter.Domain.Modules.HunterPlaces.Enums;

namespace Wayhunter.Api.Controllers.Mobile.HunterPlaces;

public sealed record HunterReviewActionRequest(string? Reason);

[ApiController]
[Authorize]
[Route("api/mobile/hunter/submissions")]
public sealed class HunterSubmissionWorkflowController : ControllerBase
{
    private readonly ITrustedIdentityAccountReader _accounts;
    private readonly HunterEvidenceService _evidence;
    private readonly IHunterEvidenceStore _evidenceStore;
    private readonly HunterSubmissionStatusService _status;
    private readonly HunterSubmissionStatusTransitionService _transitions;

    public HunterSubmissionWorkflowController(ITrustedIdentityAccountReader accounts,
        HunterEvidenceService evidence, IHunterEvidenceStore evidenceStore,
        HunterSubmissionStatusService status, HunterSubmissionStatusTransitionService transitions)
    { _accounts = accounts; _evidence = evidence; _evidenceStore = evidenceStore; _status = status; _transitions = transitions; }

    [HttpPost("{submissionId:guid}/evidence")]
    public async Task<IActionResult> BindEvidence(Guid submissionId,
        [FromBody] HunterEvidenceBindRequestDto request, CancellationToken cancellationToken)
    {
        if (!TrustedIdentityAccessContract.TryResolveUserId(User, out var userId)) return Unauthorized();
        try
        {
            var account = await _accounts.FindByUserIdAsync(userId, cancellationToken);
            if (!TrustedIdentityAccessContract.IsActiveAccountOwner(userId, account)) return Unauthorized();
            var result = await _evidence.BindAsync(userId, submissionId, request, cancellationToken);
            return result.Replayed ? Ok(new { result.EvidenceId, replayed = true }) : StatusCode(StatusCodes.Status201Created, new { result.EvidenceId });
        }
        catch (HunterMediaUnavailableException)
        { return StatusCode(503, ApiErrorResponse.Create("hunter_media_unavailable", "Trusted private media verification is not configured.", HttpContext.TraceIdentifier)); }
        catch (HunterEvidenceConflictException ex)
        { return Conflict(ApiErrorResponse.Create("hunter_evidence_conflict", ex.Message, HttpContext.TraceIdentifier)); }
        catch (ArgumentException ex)
        { return BadRequest(ApiErrorResponse.Create("hunter_evidence_invalid", ex.Message, HttpContext.TraceIdentifier)); }
        catch (UnauthorizedAccessException) { return Unauthorized(); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception)
        { return StatusCode(503, ApiErrorResponse.Create("hunter_evidence_unavailable", "Evidence could not be verified and bound.", HttpContext.TraceIdentifier)); }
    }

    [HttpPost("{submissionId:guid}/submit")]
    public async Task<IActionResult> Submit(Guid submissionId, CancellationToken cancellationToken)
    {
        if (!TrustedIdentityAccessContract.TryResolveUserId(User, out var userId)) return Unauthorized();
        try
        {
            var account = await _accounts.FindByUserIdAsync(userId, cancellationToken);
            if (!TrustedIdentityAccessContract.IsActiveAccountOwner(userId, account)) return Unauthorized();
            var own = await _status.GetMySubmissionAsync(userId, submissionId, cancellationToken);
            if (own is null) return NotFound(ApiErrorResponse.Create("hunter_submission_not_found", "Submission was not found.", HttpContext.TraceIdentifier));
            var evidenceComplete = await _evidenceStore.HasCompleteVerifiedEvidenceAsync(submissionId, userId, cancellationToken);
            if (!evidenceComplete && !_evidence.IsProviderConfigured)
                return StatusCode(503, ApiErrorResponse.Create("hunter_media_unavailable", "Trusted private media verification is not configured.", HttpContext.TraceIdentifier));
            if (!evidenceComplete)
                return Conflict(ApiErrorResponse.Create("hunter_evidence_incomplete", "Complete verified video and photo evidence is required.", HttpContext.TraceIdentifier));
            if (own.Status == HunterSubmissionStatus.PendingReview) return Ok(own);
            if (own.Status != HunterSubmissionStatus.Draft) return Conflict(ApiErrorResponse.Create("hunter_submission_not_eligible", "Submission is not a Draft.", HttpContext.TraceIdentifier));
            if (!await _transitions.SubmitDraftAsync(submissionId, cancellationToken))
                return Conflict(ApiErrorResponse.Create("hunter_submission_conflict", "Submission changed before it could be submitted.", HttpContext.TraceIdentifier));
            return Ok(await _status.GetMySubmissionAsync(userId, submissionId, cancellationToken));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception)
        { return StatusCode(503, ApiErrorResponse.Create("hunter_submission_unavailable", "Submission could not be completed.", HttpContext.TraceIdentifier)); }
    }
}

[ApiController]
[Authorize(Policy = CoinsWalletAdministratorPolicy.Name)]
[Route("api/admin/hunter/submissions")]
public sealed class AdminHunterReviewController : ControllerBase
{
    private readonly IHunterReviewService _review;
    public AdminHunterReviewController(IHunterReviewService review) => _review = review;

    [HttpPost("{submissionId:guid}/approve")]
    public Task<IActionResult> Approve(Guid submissionId, [FromBody] HunterReviewActionRequest? request,
        CancellationToken cancellationToken) => Review(submissionId, HunterSubmissionStatus.Approved, request, cancellationToken);

    [HttpPost("{submissionId:guid}/reject")]
    public Task<IActionResult> Reject(Guid submissionId, [FromBody] HunterReviewActionRequest request,
        CancellationToken cancellationToken) => Review(submissionId, HunterSubmissionStatus.Rejected, request, cancellationToken);

    private async Task<IActionResult> Review(Guid id, HunterSubmissionStatus status,
        HunterReviewActionRequest? body, CancellationToken token)
    {
        if (id == Guid.Empty) return BadRequest(ApiErrorResponse.Create("hunter_submission_invalid", "Submission ID is required.", HttpContext.TraceIdentifier));
        if (!TrustedIdentityAccessContract.TryResolveUserId(User, out var reviewer)) return Unauthorized();
        try
        {
            var result = await _review.ReviewAsync(reviewer,
                new HunterReviewRequestDto { HunterSubmissionId = id, ResultingStatus = status, Reason = body?.Reason }, token);
            return Ok(result);
        }
        catch (ArgumentException ex) { return BadRequest(ApiErrorResponse.Create("hunter_review_invalid", ex.Message, HttpContext.TraceIdentifier)); }
        catch (UnauthorizedAccessException) { return Unauthorized(); }
        catch (InvalidOperationException ex) { return Conflict(ApiErrorResponse.Create("hunter_review_conflict", ex.Message, HttpContext.TraceIdentifier)); }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception) { return StatusCode(503, ApiErrorResponse.Create("hunter_review_unavailable", "Review could not be persisted.", HttpContext.TraceIdentifier)); }
    }
}
