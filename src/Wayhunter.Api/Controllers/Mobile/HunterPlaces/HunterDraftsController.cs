using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wayhunter.Api.Contracts;
using Wayhunter.Api.Contracts.Responses;
using Wayhunter.Api.Services.IdentitySocial;
using Wayhunter.Application.Modules.HunterPlaces.Dtos;
using Wayhunter.Application.Modules.HunterPlaces.Interfaces;
using Wayhunter.Application.Modules.HunterPlaces.Services;

namespace Wayhunter.Api.Controllers.Mobile.HunterPlaces;

[ApiController]
[Authorize]
[Route("api/mobile/hunter/submissions")]
public sealed class HunterDraftsController : ControllerBase
{
    private readonly HunterDraftService _drafts;
    private readonly ITrustedIdentityAccountReader _accounts;
    public HunterDraftsController(HunterDraftService drafts, ITrustedIdentityAccountReader accounts)
    { _drafts = drafts; _accounts = accounts; }

    [HttpPost]
    [ProducesResponseType(typeof(HunterSubmissionResponseDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] HunterDraftRequestDto request, CancellationToken cancellationToken)
    {
        if (!TrustedIdentityAccessContract.TryResolveUserId(User, out var userId)) return Unauthorized();
        try
        {
            var account = await _accounts.FindByUserIdAsync(userId, cancellationToken);
            if (!TrustedIdentityAccessContract.IsActiveAccountOwner(userId, account)) return Unauthorized();
            var idempotencyKey = Request.Headers["Idempotency-Key"].ToString();
            var result = await _drafts.CreateAsync(userId, account!.DisplayName, request, idempotencyKey, cancellationToken);
            return Created($"/api/mobile/hunter/submissions/{result.SubmissionId:D}", result);
        }
        catch (ArgumentException ex) { return BadRequest(ApiErrorResponse.Create("hunter_draft_invalid", ex.Message, HttpContext.TraceIdentifier)); }
        catch (HunterIdempotencyConflictException ex) { return Conflict(ApiErrorResponse.Create("idempotency_key_conflict", ex.Message, HttpContext.TraceIdentifier)); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { return StatusCode(503, ApiErrorResponse.Create("hunter_draft_unavailable", "Draft could not be saved.", HttpContext.TraceIdentifier)); }
    }

    [HttpPut("{submissionId:guid}")]
    public async Task<IActionResult> Update(Guid submissionId, [FromBody] HunterDraftRequestDto request, CancellationToken cancellationToken)
    {
        if (!TrustedIdentityAccessContract.TryResolveUserId(User, out var userId)) return Unauthorized();
        try
        {
            var account = await _accounts.FindByUserIdAsync(userId, cancellationToken);
            if (!TrustedIdentityAccessContract.IsActiveAccountOwner(userId, account)) return Unauthorized();
            var result = await _drafts.UpdateAsync(userId, submissionId, request, cancellationToken);
            return result switch
            {
                HunterDraftUpdateResult.Updated => NoContent(),
                HunterDraftUpdateResult.NotFound => NotFound(ApiErrorResponse.Create("hunter_submission_not_found", "Draft was not found.", HttpContext.TraceIdentifier)),
                _ => Conflict(ApiErrorResponse.Create("hunter_draft_not_editable", "Only Draft submissions can be edited.", HttpContext.TraceIdentifier))
            };
        }
        catch (ArgumentException ex) { return BadRequest(ApiErrorResponse.Create("hunter_draft_invalid", ex.Message, HttpContext.TraceIdentifier)); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { return StatusCode(503, ApiErrorResponse.Create("hunter_draft_unavailable", "Draft could not be updated.", HttpContext.TraceIdentifier)); }
    }

    [HttpDelete("{submissionId:guid}")]
    public async Task<IActionResult> Delete(Guid submissionId, CancellationToken cancellationToken)
    {
        if (!TrustedIdentityAccessContract.TryResolveUserId(User, out var userId)) return Unauthorized();
        try
        {
            var account = await _accounts.FindByUserIdAsync(userId, cancellationToken);
            if (!TrustedIdentityAccessContract.IsActiveAccountOwner(userId, account)) return Unauthorized();
            var result = await _drafts.DeleteAsync(userId, submissionId, cancellationToken);
            return result switch
            {
                HunterDraftDeleteResult.Deleted => NoContent(),
                HunterDraftDeleteResult.NotFound => NotFound(ApiErrorResponse.Create("hunter_submission_not_found", "Draft was not found.", HttpContext.TraceIdentifier)),
                _ => Conflict(ApiErrorResponse.Create("hunter_draft_not_deletable", "Only an evidence-free Draft can be deleted.", HttpContext.TraceIdentifier))
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { return StatusCode(503, ApiErrorResponse.Create("hunter_draft_unavailable", "Draft could not be deleted.", HttpContext.TraceIdentifier)); }
    }
}
