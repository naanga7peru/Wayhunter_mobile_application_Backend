
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wayhunter.Api.Services.IdentitySocial;

namespace Wayhunter.Api.Controllers.Mobile.IdentitySocial;

[ApiController]
[Authorize]
[Route("api/mobile/auth")]
public sealed class SessionLogoutController : ControllerBase
{
    private readonly SessionLogoutService _logout;
    private readonly TimeProvider _clock;

    public SessionLogoutController(
        SessionLogoutService logout,
        TimeProvider clock)
    {
        _logout = logout
            ?? throw new ArgumentNullException(nameof(logout));

        _clock = clock
            ?? throw new ArgumentNullException(nameof(clock));
    }

    // POST /api/mobile/auth/logout
    // Authorization: Bearer <access_token>
    //
    // No UserId or SessionId in the request body.
    // Both identities must come from validated claims.

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(
        CancellationToken cancellationToken)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return Unauthorized();
        }

        var userId = User.FindFirst("sub")?.Value;
        var nameIdentifier = User.FindFirst(
            ClaimTypes.NameIdentifier)?.Value;

        var sessionId = User.FindFirst("sid")?.Value;

        if (!TryValidId(userId) ||
            !TryValidId(sessionId))
        {
            return Unauthorized();
        }

        // Reject inconsistent user identity claims.
        if (nameIdentifier is not null &&
            !string.Equals(
                nameIdentifier,
                userId,
                StringComparison.Ordinal))
        {
            return Unauthorized();
        }

        var revoked = await _logout.LogoutAsync(
            userId!,
            sessionId!,
            _clock.GetUtcNow(),
            cancellationToken);

        if (!revoked)
        {
            return Unauthorized();
        }

        // Successful logout.
        return NoContent();
    }

    private static bool TryValidId(string? value)
    {
        return Guid.TryParseExact(
            value, "D", out var parsed) &&
            parsed != Guid.Empty;
    }
}
