using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Wayhunter.Api.Contracts;
using Wayhunter.Api.Contracts.Responses;
using Wayhunter.Api.Contracts.Requests;
using Wayhunter.Api.Services.IdentitySocial;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Wayhunter.Infrastructure.Persistence.DbContexts;

namespace Wayhunter.Api.Controllers.Mobile.IdentitySocial;

[ApiController]
[Authorize]
[Route(IdentityApiContract.MyProfileRoute)]
public sealed class MyProfileController : ControllerBase
{
    private readonly WayhunterSharedDbContext _db;
    private readonly TimeProvider _timeProvider;

    public MyProfileController(
        WayhunterSharedDbContext db,
        TimeProvider? timeProvider = null)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    [HttpPut]
    public async Task<IActionResult> Update(
        [FromBody] UpdateMyProfileRequest request,
        CancellationToken cancellationToken)
    {
        if (!TrustedIdentityAccessContract.TryResolveUserId(
                User, out var userId))
        {
            return AuthorizationError(
                StatusCodes.Status401Unauthorized,
                "authentication_required",
                "Authentication is required.");
        }

        var account = await _db.Users.SingleOrDefaultAsync(
            x => x.UserId == userId,
            cancellationToken);

        if (!TrustedIdentityAccessContract.IsActiveAccountOwner(
                userId, account))
        {
            return AuthorizationError(
                StatusCodes.Status403Forbidden,
                "access_denied",
                "The authenticated account is not allowed to update this resource.");
        }

        if (request is null || string.IsNullOrWhiteSpace(request.DisplayName) ||
            request.DisplayName.Trim().Length > 200 ||
            request.Bio?.Length > 500)
        {
            return BadRequest(ApiErrorResponse.Create(
                "invalid_profile",
                "Display name must contain 1 to 200 characters and bio may contain at most 500 characters.",
                HttpContext.TraceIdentifier));
        }

        account!.UpdateDisplayName(request.DisplayName);
        var profile = await _db.UserProfiles.SingleOrDefaultAsync(
            x => x.UserId == userId,
            cancellationToken);
        var now = _timeProvider.GetUtcNow();
        if (profile is null)
        {
            profile = UserProfile.CreateForUser(account, now);
            _db.UserProfiles.Add(profile);
        }

        // Avatar mutation is intentionally excluded until media ownership is verified.
        profile.UpdatePublicDetails(profile.AvatarUrl, request.Bio, now);
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(ProfileReadService.GetOwnerProfile(account, User, profile));
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
