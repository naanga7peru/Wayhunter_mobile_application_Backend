using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Wayhunter.Api.Contracts;
using Wayhunter.Application.Modules.HomeAdsAnalytics.Dtos;
using Wayhunter.Application.Modules.HomeAdsAnalytics.Interfaces;
using Wayhunter.Domain.Modules.Games.Enums;
using Wayhunter.Domain.Modules.HunterPlaces.Enums;
using Wayhunter.Domain.Modules.HomeAdsAnalytics.Enums;
using Wayhunter.Domain.Modules.TravelPlanning.Enums;
using Wayhunter.Infrastructure.Persistence.DbContexts;

namespace Wayhunter.Api.Controllers.Mobile.HomeAdsAnalytics;

/// <summary>
/// Minimal authenticated Home projection composed only from existing M1-M7
/// read models. It deliberately excludes contact, GPS, media and analytics data.
/// </summary>
[ApiController]
[Authorize]
[Route("api/mobile/home")]
public sealed class HomeDashboardController : ControllerBase
{
    private readonly WayhunterSharedDbContext _db;
    private readonly IAdvertisementRepository _advertisements;
    private readonly TimeProvider _clock;

    public HomeDashboardController(
        WayhunterSharedDbContext db,
        IAdvertisementRepository advertisements,
        TimeProvider clock)
    {
        _db = db;
        _advertisements = advertisements;
        _clock = clock;
    }

    [HttpGet]
    [ProducesResponseType<HomeDashboardResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<HomeDashboardResponse>> Get(CancellationToken ct)
    {
        if (!TrustedIdentityAccessContract.TryResolveUserId(User, out var userId))
            return Unauthorized();

        var account = await _db.Users.AsNoTracking()
            .SingleOrDefaultAsync(x => x.UserId == userId, ct);
        if (!TrustedIdentityAccessContract.IsActiveAccountOwner(userId, account))
            return Forbid();

        var profile = await _db.UserProfiles.AsNoTracking()
            .SingleOrDefaultAsync(x => x.UserId == userId, ct);
        var now = _clock.GetUtcNow().ToUniversalTime();
        var wallet = await _db.CoinWallets.AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => new HomeWalletSummary(
                x.PostedCoins, x.ReservedCoins, x.PostedCoins - x.ReservedCoins,
                x.LifetimeEarnedCoins))
            .SingleOrDefaultAsync(ct);
        var today = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);
        var trips = await _db.Trips.AsNoTracking()
            .Where(x => x.OwnerUserId == userId && x.EndDate >= today &&
                x.DraftStatus != TripDraftStatus.Cancelled)
            .OrderBy(x => x.StartDate)
            .Take(5)
            .Select(x => new HomeTripSummary(x.Id, x.TripName, x.StartDate, x.EndDate,
                x.Stops.Count))
            .ToArrayAsync(ct);
        var games = await _db.Games.AsNoTracking()
            .Where(x => x.Status == GameStatus.Published &&
                x.ExpiresAtUtc != null && x.ExpiresAtUtc > now)
            .OrderByDescending(x => x.ApprovedAtUtc)
            .Take(5)
            .Select(x => new HomeGameSummary(x.Id, x.Title, x.Description,
                x.DurationMinutes, x.ExpiresAtUtc))
            .ToArrayAsync(ct);
        var approvedPlaces = await _db.HunterSubmissions.AsNoTracking()
            .Where(x => x.Status == HunterSubmissionStatus.Approved)
            .OrderBy(x => x.PlaceName)
            .Take(5)
            .Select(x => new HomePlaceSummary(x.Id, x.PlaceName, x.District,
                x.Latitude, x.Longitude))
            .ToArrayAsync(ct);

        var ads = await _advertisements.GetActiveAsync(now, cancellationToken: ct);
        return Ok(new HomeDashboardResponse(
            new HomeProfileSummary(userId, account!.DisplayName, profile?.Bio),
            wallet,
            trips,
            games,
            approvedPlaces,
            ads.Where(x => x.Placement == AdvertisementPlacement.HomeBanner).ToArray(),
            ads.Where(x => x.Placement == AdvertisementPlacement.SponsoredPoi).ToArray()));
    }
}

public sealed record HomeDashboardResponse(
    HomeProfileSummary Profile,
    HomeWalletSummary? Wallet,
    IReadOnlyList<HomeTripSummary> UpcomingTrips,
    IReadOnlyList<HomeGameSummary> AvailableGames,
    IReadOnlyList<HomePlaceSummary> ApprovedHunterPlaces,
    IReadOnlyList<AdvertisementDto> HomeBanners,
    IReadOnlyList<AdvertisementDto> SponsoredPois);

public sealed record HomeProfileSummary(string UserId, string DisplayName, string? Bio);
public sealed record HomeWalletSummary(long PostedCoins, long ReservedCoins,
    long SpendableCoins, long LifetimeEarnedCoins);
public sealed record HomeTripSummary(Guid TripPlanId, string Name,
    DateOnly StartDate, DateOnly EndDate, int StopCount);
public sealed record HomeGameSummary(Guid GameId, string? Title, string? Description,
    int? DurationMinutes, DateTimeOffset? ExpiresAtUtc);
public sealed record HomePlaceSummary(Guid SubmissionId, string PlaceName,
    string District, double Latitude, double Longitude);
