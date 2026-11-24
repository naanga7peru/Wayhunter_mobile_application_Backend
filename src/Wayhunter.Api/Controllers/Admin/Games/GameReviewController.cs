using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wayhunter.Api.Contracts;
using Wayhunter.Api.Extensions;
using Wayhunter.Application.Modules.Games.Dtos;
using Wayhunter.Application.Modules.Games.Services;
using Wayhunter.Infrastructure.Persistence.Repositories.Games;

namespace Wayhunter.Api.Controllers.Admin.Games;

[ApiController]
[Authorize(Policy = AdminSafetyServiceCollectionExtensions.Policy)]
[Route("api/admin/games/reviews")]
public sealed class GameReviewController : ControllerBase
{
    private readonly SqlGameAdminReviewStore _store;
    private readonly GameApprovalService _approvals;
    private readonly GameModerationService _moderation;
    private readonly TimeProvider _clock;

    public GameReviewController(SqlGameAdminReviewStore store, GameApprovalService approvals,
        GameModerationService moderation, TimeProvider clock)
    {
        _store = store;
        _approvals = approvals;
        _moderation = moderation;
        _clock = clock;
    }

    [HttpGet("pending")]
    public async Task<IActionResult> Pending(CancellationToken ct) =>
        Ok(await _store.GetPendingAsync(ct));

    [HttpPost("{gameId:guid}/approve")]
    public async Task<IActionResult> Approve(Guid gameId, CancellationToken ct)
    {
        if (!TrustedIdentityAccessContract.TryResolveUserId(User, out var adminId))
            return Unauthorized();
        var now = _clock.GetUtcNow().ToUniversalTime();
        try
        {
            var reward = await _approvals.ApproveAsync(new GameApprovalCommand(
                gameId, adminId, Guid.NewGuid().ToString("D"), now,
                Guid.NewGuid(), Guid.NewGuid(), "M5-Game-Creation-v1"), ct);
            return Ok(new { gameId, status = "Published", approvedAtUtc = now,
                expiresAtUtc = now.AddDays(30), creatorRewardEventId = reward.EventId });
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { error = ex.Message }); }
    }

    [HttpPost("{gameId:guid}/reject")]
    public async Task<IActionResult> Reject(Guid gameId, [FromBody] GameReviewRejectionRequest request,
        CancellationToken ct)
    {
        if (!TrustedIdentityAccessContract.TryResolveUserId(User, out var adminId))
            return Unauthorized();
        var now = _clock.GetUtcNow().ToUniversalTime();
        try
        {
            var audit = await _moderation.ModerateAsync(new GameModerationCommand(
                Guid.NewGuid(), gameId, adminId,
                Wayhunter.Domain.Modules.Games.Enums.GameModerationAction.Reject,
                request.Reason, now, Guid.NewGuid()), ct);
            return Ok(audit);
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { error = ex.Message }); }
    }
}

public sealed record GameReviewRejectionRequest(string Reason);
