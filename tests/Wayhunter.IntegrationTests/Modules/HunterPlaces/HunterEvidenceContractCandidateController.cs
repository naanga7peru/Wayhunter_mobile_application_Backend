using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wayhunter.Api.Contracts;
using Wayhunter.Application.Modules.HunterPlaces.Services;
using Wayhunter.Domain.Modules.HunterPlaces.Enums;

namespace Wayhunter.IntegrationTests.Modules.HunterPlaces;

[ApiController]
[Authorize]
[Route("api/mobile/v1/hunter/drafts/{id:guid}/capture-sessions/{sessionId:guid}")]
public sealed class HunterEvidenceContractCandidateController : ControllerBase
{
    [HttpPost("uploads")]
    public Task<IActionResult> Upload(Guid id, Guid sessionId, UploadRequest request,
        [FromServices] HunterEvidenceWorkflowCandidate workflow, CancellationToken ct) =>
        Execute(user => workflow.AuthorizeAsync(user, id, sessionId, request.EvidenceType, request.ContentType, ct));

    [HttpPost("evidence")]
    public Task<IActionResult> Complete(Guid id, Guid sessionId, CompletionRequest request,
        [FromServices] HunterEvidenceWorkflowCandidate workflow, CancellationToken ct) =>
        Execute(user => workflow.CompleteAsync(user, id, sessionId, request.EvidenceType, request.UploadId, ct));

    private async Task<IActionResult> Execute<T>(Func<string, Task<T?>> action) where T : class
    {
        if (!TrustedIdentityAccessContract.TryResolveUserId(User, out var userId))
            return Unauthorized();

        try
        {
            var result = await action(userId);
            return result is null ? Conflict() : StatusCode(201, result);
        }
        catch (UnauthorizedAccessException)
        {
            return NotFound();
        }
        catch (ArgumentException)
        {
            return BadRequest();
        }
    }

    public sealed record UploadRequest(HunterEvidenceType EvidenceType, string ContentType);
    // Evidence type remains provisional until an approved upload record can resolve it.
    public sealed record CompletionRequest(Guid UploadId, HunterEvidenceType EvidenceType);
}
