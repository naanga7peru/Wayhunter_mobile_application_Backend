using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Wayhunter.Api.Contracts;
using Wayhunter.Api.Services.IdentitySocial;
using Wayhunter.Infrastructure.Persistence.DbContexts;

namespace Wayhunter.Api.Controllers.Mobile.IdentitySocial;

[ApiController]
[Authorize]
[Route("api/mobile/identity/me")]
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
            return Unauthorized();
        }

        var account = await _db.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.UserId == userId,
                cancellationToken);

        if (!TrustedIdentityAccessContract.IsActiveAccountOwner(
                userId, account))
        {
            return Forbid();
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
            return Unauthorized();
        }
    }
}
