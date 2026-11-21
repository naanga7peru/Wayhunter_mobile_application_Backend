
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wayhunter.Api.Controllers.Mobile.IdentitySocial;
using Wayhunter.Api.Contracts.Responses;
using Wayhunter.Api.Services.IdentitySocial;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Xunit;

namespace Wayhunter.UnitTests.Modules.IdentitySocial;

public sealed class SessionLogoutControllerTests
{
    private sealed class FakeRevocationStore
        : IIdentitySessionRevocationStore
    {
        public IdentitySession? Session { get; set; }

        public int RevokeCount { get; private set; }

        public Task<IdentitySession?> FindAsync(
            string sessionId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(
                Session?.SessionId == sessionId
                    ? Session
                    : null);
        }

        public Task<SessionRevocationWriteResult> TryRevokeAsync(
            string userId,
            string sessionId,
            DateTimeOffset revokedAtUtc,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (Session is null ||
                Session.SessionId != sessionId ||
                Session.UserId != userId)
            {
                return Task.FromResult(
                    SessionRevocationWriteResult.NotFound);
            }

            if (Session.RevokedAtUtc.HasValue)
            {
                return Task.FromResult(
                    SessionRevocationWriteResult.AlreadyRevoked);
            }

            Session.Revoke(revokedAtUtc);
            RevokeCount++;

            return Task.FromResult(
                SessionRevocationWriteResult.Revoked);
        }
    }

    private static (
        SessionLogoutController Controller,
        FakeRevocationStore Store,
        string UserId,
        string SessionId) Create(
            bool authenticated = true,
            bool includeSession = true,
            bool conflictingUserClaim = false)
    {
        var now = DateTimeOffset.UtcNow;

        var userId = Guid.NewGuid().ToString("D");

        var session = IdentitySession.Create(
            userId,
            now.AddMinutes(-10),
            now.AddHours(1));

        var store = new FakeRevocationStore
        {
            Session = session
        };

        var claims = new List<Claim>
        {
            new("sub", userId),
            new(
                ClaimTypes.NameIdentifier,
                conflictingUserClaim
                    ? Guid.NewGuid().ToString("D")
                    : userId)
        };

        if (includeSession)
        {
            claims.Add(new Claim("sid", session.SessionId));
        }

        var principal = new ClaimsPrincipal(
            new ClaimsIdentity(
                claims,
                authenticated ? "SessionBearer" : null));

        var controller = new SessionLogoutController(
            new SessionLogoutService(store),
            TimeProvider.System)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = principal
                }
            }
        };

        return (controller, store, userId, session.SessionId);
    }

    [Fact]
    public async Task ValidSession_LogoutReturns204()
    {
        var test = Create();

        var result = await test.Controller.Logout(
            CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        Assert.Equal(1, test.Store.RevokeCount);
        Assert.NotNull(test.Store.Session?.RevokedAtUtc);
    }

    [Fact]
    public async Task AnonymousRequest_IsRejected()
    {
        var test = Create(authenticated: false);

        var result = await test.Controller.Logout(
            CancellationToken.None);

        AssertApiError(test.Controller, result, 401, "authentication_required");
        Assert.Equal(0, test.Store.RevokeCount);
    }

    [Fact]
    public async Task MissingSessionClaim_IsRejected()
    {
        var test = Create(includeSession: false);

        var result = await test.Controller.Logout(
            CancellationToken.None);

        AssertApiError(test.Controller, result, 401, "authentication_required");
        Assert.Equal(0, test.Store.RevokeCount);
    }

    [Fact]
    public async Task ConflictingUserClaims_AreRejected()
    {
        var test = Create(conflictingUserClaim: true);

        var result = await test.Controller.Logout(
            CancellationToken.None);

        AssertApiError(test.Controller, result, 401, "authentication_required");
        Assert.Equal(0, test.Store.RevokeCount);
    }

    [Fact]
    public async Task MissingServerSession_IsRejected()
    {
        var test = Create();
        test.Store.Session = null;

        var result = await test.Controller.Logout(
            CancellationToken.None);

        AssertApiError(test.Controller, result, 401, "authentication_required");
        Assert.Equal(0, test.Store.RevokeCount);
    }

    private static void AssertApiError(
        ControllerBase controller,
        IActionResult result,
        int expectedStatusCode,
        string expectedCode)
    {
        var response = Assert.IsType<ObjectResult>(result);
        Assert.Equal(expectedStatusCode, response.StatusCode);

        var error = Assert.IsType<ApiErrorResponse>(response.Value);
        Assert.Equal(expectedCode, error.Code);
        Assert.False(string.IsNullOrWhiteSpace(error.Message));
        Assert.Equal(controller.HttpContext.TraceIdentifier, error.CorrelationId);
        Assert.False(string.IsNullOrWhiteSpace(error.CorrelationId));
    }
}
