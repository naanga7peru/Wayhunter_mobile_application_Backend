
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.HomeAdsAnalytics.Dtos;
using Wayhunter.Application.Modules.HomeAdsAnalytics.Interfaces;
using Wayhunter.Application.Modules.HomeAdsAnalytics.Services;
using Wayhunter.Domain.Modules.HomeAdsAnalytics.Enums;
using Xunit;

namespace Wayhunter.UnitTests.Modules.HomeAdsAnalytics;

public sealed class AdvertisementServiceTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 11, 13, 12, 0, 0, TimeSpan.Zero);

    // Existing Test 1
    [Fact]
    public async Task ActiveAd_IsReturned()
    {
        var ad = CreateAd();

        var result = await CreateService(ad).GetActiveAsync(Now);

        Assert.Single(result);
        Assert.Equal(ad.Id, result[0].Id);
    }

    // Existing Test 2
    [Fact]
    public async Task DisabledAd_IsExcluded()
    {
        var ad = CreateAd(enabled: false);

        var result = await CreateService(ad).GetActiveAsync(Now);

        Assert.Empty(result);
    }

    // Existing Test 3
    [Fact]
    public async Task FutureAd_IsExcluded()
    {
        var ad = CreateAd(
            startMinutes: 10,
            endMinutes: 20);

        var result = await CreateService(ad).GetActiveAsync(Now);

        Assert.Empty(result);
    }

    // Existing Test 4
    [Fact]
    public async Task ExpiredAd_IsExcluded()
    {
        var ad = CreateAd(
            startMinutes: -20,
            endMinutes: -10);

        var result = await CreateService(ad).GetActiveAsync(Now);

        Assert.Empty(result);
    }

    // Existing Test 5
    [Fact]
    public async Task AdAtExactExpiry_IsExcluded()
    {
        var ad = CreateAd(
            startMinutes: -10,
            endMinutes: 0);

        var result = await CreateService(ad).GetActiveAsync(Now);

        Assert.Empty(result);
    }

    // New Test 6
    [Fact]
    public async Task HomeBanner_ShouldReturnOnlyHomeBannerAds()
    {
        var homeAd = CreateAd() with
        {
            Placement = AdvertisementPlacement.HomeBanner
        };

        var poiAd = CreateAd() with
        {
            Placement = AdvertisementPlacement.SponsoredPoi
        };

        var service = CreateService(homeAd, poiAd);

        var result = await service.GetActiveByPlacementAsync(
            Now,
            AdvertisementPlacement.HomeBanner);

        var returnedAd = Assert.Single(result);

        Assert.Equal(homeAd.Id, returnedAd.Id);
    }

    // New Test 7
    [Fact]
    public async Task SponsoredPoi_ShouldReturnOnlySponsoredPoiAds()
    {
        var homeAd = CreateAd() with
        {
            Placement = AdvertisementPlacement.HomeBanner
        };

        var poiAd = CreateAd() with
        {
            Placement = AdvertisementPlacement.SponsoredPoi
        };

        var service = CreateService(homeAd, poiAd);

        var result = await service.GetActiveByPlacementAsync(
            Now,
            AdvertisementPlacement.SponsoredPoi);

        var returnedAd = Assert.Single(result);

        Assert.Equal(poiAd.Id, returnedAd.Id);
    }

    // New Test 8
    [Fact]
    public async Task DisabledAd_ShouldNotAppearInPlacementResults()
    {
        var disabledAd = CreateAd(enabled: false) with
        {
            Placement = AdvertisementPlacement.HomeBanner
        };

        var service = CreateService(disabledAd);

        var result = await service.GetActiveByPlacementAsync(
            Now,
            AdvertisementPlacement.HomeBanner);

        Assert.Empty(result);
    }

    // New Tests 9 and 10
    [Theory]
    [InlineData(0)]
    [InlineData(999)]
    public async Task InvalidPlacement_ShouldBeRejected(
        int placement)
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => service.GetActiveByPlacementAsync(
                Now,
                (AdvertisementPlacement)placement));
    }

    // Existing Helper Method
    private static AdvertisementDto CreateAd(
        bool enabled = true,
        int startMinutes = -10,
        int endMinutes = 10)
    {
        return new AdvertisementDto
        {
            Id = Guid.NewGuid(),
            Title = "Wayhunter Test Ad",
            ImageUrl = "https://example.com/test.jpg",
            StartAtUtc = Now.AddMinutes(startMinutes),
            EndAtUtc = Now.AddMinutes(endMinutes),
            IsEnabled = enabled
        };
    }

    // Existing Helper Method
    private static AdvertisementService CreateService(
        params AdvertisementDto[] ads)
    {
        return new AdvertisementService(
            new FakeAdvertisementRepository(ads));
    }

    // Existing Fake Repository
    private sealed class FakeAdvertisementRepository
        : IAdvertisementRepository
    {
        private readonly IReadOnlyList<AdvertisementDto> _ads;

        public FakeAdvertisementRepository(
            IReadOnlyList<AdvertisementDto> ads)
        {
            _ads = ads;
        }

        public Task<IReadOnlyList<AdvertisementDto>> GetAllAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_ads);
        }

        public Task<AdvertisementDto?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                _ads.FirstOrDefault(ad => ad.Id == id));
        }
    }
}
