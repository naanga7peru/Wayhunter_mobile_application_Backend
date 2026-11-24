using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Wayhunter.Api.Controllers.Mobile.HomeAdsAnalytics;
using Wayhunter.Application.Modules.HomeAdsAnalytics.Dtos;
using Wayhunter.Application.Modules.HomeAdsAnalytics.Interfaces;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Wayhunter.Domain.Modules.HomeAdsAnalytics.Enums;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Wayhunter.Infrastructure.Persistence.Models.CoinsWallet;
using Xunit;

namespace Wayhunter.UnitTests.Modules.HomeAdsAnalytics;

public sealed class HomeDashboardControllerTests
{
    [Fact]
    public async Task Get_RejectsUnauthenticatedIdentityBeforeReadingPersonalData()
    {
        var options = new DbContextOptionsBuilder<WayhunterSharedDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options;
        await using var db = new WayhunterSharedDbContext(options);
        var controller = new HomeDashboardController(
            db, new Ads(), new FrozenTimeProvider(DateTimeOffset.UtcNow))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var result = await controller.Get(default);
        Assert.IsType<UnauthorizedResult>(result.Result);
    }

    [Fact]
    public async Task Get_ComposesOnlyAuthenticatedOwnerDataAndActiveAdPlacements()
    {
        var now = new DateTimeOffset(2026, 11, 24, 10, 0, 0, TimeSpan.Zero);
        var options = new DbContextOptionsBuilder<WayhunterSharedDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options;
        await using var db = new WayhunterSharedDbContext(options);
        var account = UserAccount.RegisterPending("Demo Traveller",
            $"home-{Guid.NewGuid():N}@example.invalid", "test-only-hash", now.AddDays(-1));
        account.ActivateAfterVerifiedOtp(now.AddHours(-1));
        db.Users.Add(account);
        db.UserProfiles.Add(UserProfile.CreateForUser(account, now.AddMinutes(-30)));
        db.CoinWallets.Add(new CoinWalletRow
        {
            Id = Guid.NewGuid(), UserId = account.UserId, PostedCoins = 500,
            ReservedCoins = 100, LifetimeEarnedCoins = 700
        });
        await db.SaveChangesAsync();

        var controller = new HomeDashboardController(
            db, new Ads(), new FrozenTimeProvider(now))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, account.UserId)], "test"))
                }
            }
        };

        var result = await controller.Get(default);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<HomeDashboardResponse>(ok.Value);
        Assert.Equal(account.UserId, response.Profile.UserId);
        Assert.Equal(500, response.Wallet!.PostedCoins);
        Assert.Equal(100, response.Wallet.ReservedCoins);
        Assert.Equal(400, response.Wallet.SpendableCoins);
        Assert.Single(response.HomeBanners);
        Assert.Single(response.SponsoredPois);
        Assert.Empty(response.UpcomingTrips);
    }

    private sealed class Ads : IAdvertisementRepository
    {
        public Task<IReadOnlyList<AdvertisementDto>> GetActiveAsync(
            DateTimeOffset nowUtc, AdvertisementPlacement? placement = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AdvertisementDto>>(
            [new AdvertisementDto { Id = Guid.NewGuid(), Placement = AdvertisementPlacement.HomeBanner },
             new AdvertisementDto { Id = Guid.NewGuid(), Placement = AdvertisementPlacement.SponsoredPoi }]);

        public Task<IReadOnlyList<AdvertisementDto>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AdvertisementDto>>([]);

        public Task<AdvertisementDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<AdvertisementDto?>(null);
    }

    private sealed class FrozenTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
