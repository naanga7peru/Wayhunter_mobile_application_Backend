using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wayhunter.Api.Contracts;
using Wayhunter.Api.Services.IdentitySocial;

namespace Wayhunter.Api.Controllers.Mobile.IdentitySocial;

[ApiController]
[Authorize]
[Route("api/mobile/identity/blocks/{targetUserId}")]
public sealed class BlockedUsersController(BlockedUserRelationshipService service) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Block(string targetUserId, CancellationToken ct)
    {
        if (!TrustedIdentityAccessContract.TryResolveUserId(User, out var actor)) return Unauthorized();
        if (!TryCanonical(targetUserId, out var target)) return BadRequest(new { message = "A valid target user ID is required." });
        try
        {
            await service.BlockAsync(actor, target, ct);
            return NoContent();
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Unblock(string targetUserId, CancellationToken ct)
    {
        if (!TrustedIdentityAccessContract.TryResolveUserId(User, out var actor)) return Unauthorized();
        if (!TryCanonical(targetUserId, out var target)) return BadRequest(new { message = "A valid target user ID is required." });
        try
        {
            await service.UnblockAsync(actor, target, ct);
            return NoContent();
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    private static bool TryCanonical(string value, out string userId)
    {
        userId = string.Empty;
        if (!Guid.TryParseExact(value, "D", out var id) || id == Guid.Empty) return false;
        userId = id.ToString("D");
        return true;
    }
}
