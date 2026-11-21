using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Wayhunter.Api.Contracts;
using Wayhunter.Api.Contracts.Responses;
using Wayhunter.Api.Services.IdentitySocial;
using Wayhunter.Infrastructure.Persistence.DbContexts;

namespace Wayhunter.Api.Controllers.Mobile.IdentitySocial;

[ApiController]
[Authorize]
[Route(IdentityApiContract.MyProfileRoute)]
public sealed class MyProfileController : ControllerBase
{
    private readonly WayhunterSharedDbContext _db;

    public MyProfileController(WayhunterSharedDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        if (!TrustedIdentityAccessContract.TryResolveUserId(
                User, out var userId))
        {
            return AuthorizationError(
                StatusCodes.Status401Unauthorized,
                "authentication_required",
                "Authentication is required.");
        }

        var account = await _db.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.UserId == userId,
                cancellationToken);

        if (!TrustedIdentityAccessContract.IsActiveAccountOwner(
                userId, account))
        {
            return AuthorizationError(
                StatusCodes.Status403Forbidden,
                "access_denied",
                "The authenticated account is not allowed to access this resource.");
        }

        var profile = await _db.UserProfiles
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.UserId == userId,
                cancellationToken);

        try
        {
            return Ok(ProfileReadService.GetOwnerProfile(
                account!, User, profile));
        }
        catch (UnauthorizedAccessException)
        {
            return AuthorizationError(
                StatusCodes.Status401Unauthorized,
                "authentication_required",
                "Authentication is required.");
        }
    }

    private ObjectResult AuthorizationError(
        int statusCode, string code, string message) =>
        StatusCode(statusCode, ApiErrorResponse.Create(
            code, message, HttpContext.TraceIdentifier));
}
