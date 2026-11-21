using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wayhunter.Api.Contracts;
using Wayhunter.Application.Modules.HunterPlaces.Services;

namespace Wayhunter.IntegrationTests.Modules.HunterPlaces;

[ApiController]
[Authorize(Policy = HunterReviewContractCandidateController.Policy)]
[Route("api/admin/v1/hunter/places")]
public sealed class HunterModerationContractCandidateController : ControllerBase
{
    [HttpPut("{id:guid}/details")]
    public Task<IActionResult> Update(Guid id, Details request,
        [FromServices] IHunterPlaceModerationCandidate moderation, CancellationToken ct) =>
        Execute(id, !string.IsNullOrWhiteSpace(request.Name),
            reviewer => moderation.UpdateAsync(reviewer, id, request.Name.Trim(), request.Description?.Trim(), ct));

    [HttpPost("{id:guid}/deactivate")]
    public Task<IActionResult> Deactivate(Guid id, Deactivation request,
        [FromServices] IHunterPlaceModerationCandidate moderation, CancellationToken ct) =>
        Execute(id, !string.IsNullOrWhiteSpace(request.Reason),
            reviewer => moderation.DeactivateAsync(reviewer, id, request.Reason.Trim(), ct));

    private async Task<IActionResult> Execute(Guid id, bool valid, Func<string, Task<bool>> action)
    {
        if (!TrustedIdentityAccessContract.TryResolveUserId(User, out var reviewer))
            return Unauthorized();

        if (id == Guid.Empty || !valid)
            return BadRequest();

        return await action(reviewer) ? NoContent() : NotFound();
    }

    public sealed record Details(string Name, string? Description);
    public sealed record Deactivation(string Reason);
}
