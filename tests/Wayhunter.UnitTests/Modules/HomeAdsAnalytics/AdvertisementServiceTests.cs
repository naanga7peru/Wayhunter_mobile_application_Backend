
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

    // Existing Test 6
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

    // Existing Test 7
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

    // Existing Test 8
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

    // Existing Tests 9 and 10
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

    // Existing Test 11
    [Fact]
    public async Task EmptyQuery_ShouldReturnOnlyActiveAds()
    {
        var activeAd = CreateAd();
        var disabledAd = CreateAd(enabled: false);

        var service = CreateService(activeAd, disabledAd);
        var request = new AdvertisementActiveQueryRequest();

        var result = await service.GetActiveByQueryAsync(
            request,
            Now);

        var returnedAd = Assert.Single(result);

        Assert.Equal(activeAd.Id, returnedAd.Id);
    }

    // Existing Tests 12 and 13
    [Theory]
    [InlineData(AdvertisementPlacement.HomeBanner)]
    [InlineData(AdvertisementPlacement.SponsoredPoi)]
    public async Task Query_ShouldFilterByPlacement(
        AdvertisementPlacement placement)
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

        var request = new AdvertisementActiveQueryRequest
        {
            Placement = placement
        };

        var result = await service.GetActiveByQueryAsync(
            request,
            Now);

        var returnedAd = Assert.Single(result);

        Assert.Equal(placement, returnedAd.Placement);
    }

    // Existing Test 14
    [Fact]
    public async Task Query_ShouldUseSpecifiedUtcTime()
    {
        var futureAd = CreateAd(
            startMinutes: 10,
            endMinutes: 30);

        var service = CreateService(futureAd);

        var request = new AdvertisementActiveQueryRequest
        {
            AtUtc = Now.AddMinutes(15)
        };

        var result = await service.GetActiveByQueryAsync(
            request,
            Now);

        Assert.Single(result);
    }

    // Existing Test 15
    [Fact]
    public async Task NullQuery_ShouldBeRejected()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.GetActiveByQueryAsync(
                null,
                Now));
    }

    // Existing Test 16
    [Fact]
    public async Task InvalidPlacementQuery_ShouldBeRejected()
    {
        var service = CreateService();

        var request = new AdvertisementActiveQueryRequest
        {
            Placement = (AdvertisementPlacement)999
        };

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.GetActiveByQueryAsync(
                request,
                Now));
    }

    // Existing Test 17
    [Fact]
    public async Task DistrictQuery_ShouldRejectUntilSupported()
    {
        var service = CreateService();

        var request = new AdvertisementActiveQueryRequest
        {
            District = "Jaffna"
        };

        await Assert.ThrowsAsync<NotSupportedException>(
            () => service.GetActiveByQueryAsync(
                request,
                Now));
    }

    // Existing Test 18
    [Fact]
    public async Task InvalidDistrictQuery_ShouldBeRejected()
    {
        var service = CreateService();

        var request = new AdvertisementActiveQueryRequest
        {
            District = "London"
        };

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.GetActiveByQueryAsync(
                request,
                Now));
    }

    // Existing Tests 19, 20 and 21
    [Theory]
    [InlineData(AdvertisementStatus.Draft)]
    [InlineData(AdvertisementStatus.Paused)]
    [InlineData(AdvertisementStatus.Archived)]
    public async Task NonActiveStatus_ShouldNotBeDisplayed(
        AdvertisementStatus status)
    {
        var ad = CreateAd() with
        {
            Status = status,
            IsEnabled = true
        };

        var service = CreateService(ad);

        var result = await service.GetActiveAsync(Now);

        Assert.Empty(result);
    }

    // NEW Tests 22, 23 and 24
    [Theory]
    [InlineData(AdvertisementStatus.Draft)]
    [InlineData(AdvertisementStatus.Paused)]
    [InlineData(AdvertisementStatus.Archived)]
    public async Task NonActiveStatus_ShouldBeExcludedFromPlacementResults(
        AdvertisementStatus status)
    {
        var ad = CreateAd() with
        {
            Placement = AdvertisementPlacement.HomeBanner,
            Status = status,
            IsEnabled = true
        };

        var service = CreateService(ad);

        var result = await service.GetActiveByPlacementAsync(
            Now,
            AdvertisementPlacement.HomeBanner);

        Assert.Empty(result);
    }

    // NEW Tests 25, 26 and 27
    [Theory]
    [InlineData(AdvertisementStatus.Draft)]
    [InlineData(AdvertisementStatus.Paused)]
    [InlineData(AdvertisementStatus.Archived)]
    public async Task NonActiveStatus_ShouldBeExcludedFromQueryResults(
        AdvertisementStatus status)
    {
        var ad = CreateAd() with
        {
            Placement = AdvertisementPlacement.SponsoredPoi,
            Status = status,
            IsEnabled = true
        };

        var service = CreateService(ad);

        var request = new AdvertisementActiveQueryRequest
        {
            Placement = AdvertisementPlacement.SponsoredPoi
        };

        var result = await service.GetActiveByQueryAsync(
            request,
            Now);

        Assert.Empty(result);
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
            IsEnabled = enabled,
            Status = enabled
                ? AdvertisementStatus.Active
                : AdvertisementStatus.Paused
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
