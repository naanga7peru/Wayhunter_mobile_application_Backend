
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

    // TEST 01 - Active advertisement
    [Fact]
    public async Task ActiveAd_IsReturned()
    {
        var ad = CreateAd();

        var result = await CreateService(ad).GetActiveAsync(Now);

        Assert.Single(result);
        Assert.Equal(ad.Id, result[0].Id);
    }

    // TEST 02 - Disabled advertisement
    [Fact]
    public async Task DisabledAd_IsExcluded()
    {
        var ad = CreateAd(enabled: false);

        var result = await CreateService(ad).GetActiveAsync(Now);

        Assert.Empty(result);
    }

    // TEST 03 - Future advertisement
    [Fact]
    public async Task FutureAd_IsExcluded()
    {
        var ad = CreateAd(
            startMinutes: 10,
            endMinutes: 20);

        var result = await CreateService(ad).GetActiveAsync(Now);

        Assert.Empty(result);
    }

    // TEST 04 - Expired advertisement
    [Fact]
    public async Task ExpiredAd_IsExcluded()
    {
        var ad = CreateAd(
            startMinutes: -20,
            endMinutes: -10);

        var result = await CreateService(ad).GetActiveAsync(Now);

        Assert.Empty(result);
    }

    // TEST 05 - Exact expiry boundary
    [Fact]
    public async Task AdAtExactExpiry_IsExcluded()
    {
        var ad = CreateAd(
            startMinutes: -10,
            endMinutes: 0);

        var result = await CreateService(ad).GetActiveAsync(Now);

        Assert.Empty(result);
    }

    // TEST 06 - Home banner placement
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

    // TEST 07 - Sponsored POI placement
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

    // TEST 08 - Disabled placement
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

    // TESTS 09-10 - Invalid placement
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

    // TEST 11 - Empty query
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

    // TESTS 12-13 - Query placement filtering
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

    // TEST 14 - Query UTC time
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

    // TEST 15 - Null query
    [Fact]
    public async Task NullQuery_ShouldBeRejected()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.GetActiveByQueryAsync(
                null,
                Now));
    }

    // TEST 16 - Invalid placement query
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

    // TEST 17 - District fail-closed
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

    // TEST 18 - Invalid district
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

    // TESTS 19-21 - Non-active lifecycle status
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

    // TESTS 22-24 - Non-active placement results
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

    // TESTS 25-27 - Non-active query results
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

    // TEST 28 - Exact start boundary
    [Fact]
    public async Task AdAtExactStart_ShouldBeIncluded()
    {
        var ad = CreateAd(
            startMinutes: 0,
            endMinutes: 10);

        var service = CreateService(ad);

        var result = await service.GetActiveAsync(Now);

        var returnedAd = Assert.Single(result);

        Assert.Equal(ad.Id, returnedAd.Id);
    }

    // TEST 29 - Combined UTC time and placement
    [Fact]
    public async Task Query_ShouldApplyTimeAndPlacementTogether()
    {
        var homeAd = CreateAd(
            startMinutes: 10,
            endMinutes: 30) with
        {
            Placement = AdvertisementPlacement.HomeBanner
        };

        var poiAd = CreateAd(
            startMinutes: 10,
            endMinutes: 30) with
        {
            Placement = AdvertisementPlacement.SponsoredPoi
        };

        var service = CreateService(homeAd, poiAd);

        var request = new AdvertisementActiveQueryRequest
        {
            AtUtc = Now.AddMinutes(15),
            Placement = AdvertisementPlacement.HomeBanner
        };

        var result = await service.GetActiveByQueryAsync(
            request,
            Now);

        var returnedAd = Assert.Single(result);

        Assert.Equal(homeAd.Id, returnedAd.Id);
    }

    // TEST 30 - Invalid query does not read repository
    [Fact]
    public async Task InvalidQuery_ShouldNotAccessRepository()
    {
        var repository = new RecordingAdvertisementRepository();

        var service = new AdvertisementService(repository);

        var request = new AdvertisementActiveQueryRequest
        {
            Placement = (AdvertisementPlacement)999
        };

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.GetActiveByQueryAsync(
                request,
                Now));

        Assert.Equal(0, repository.ReadCount);
    }

    // TEST 31 - District query does not read repository
    [Fact]
    public async Task DistrictQuery_ShouldNotAccessRepository()
    {
        var repository = new RecordingAdvertisementRepository();

        var service = new AdvertisementService(repository);

        var request = new AdvertisementActiveQueryRequest
        {
            District = "Jaffna"
        };

        await Assert.ThrowsAsync<NotSupportedException>(
            () => service.GetActiveByQueryAsync(
                request,
                Now));

        Assert.Equal(0, repository.ReadCount);
    }

    // TEST 32 - CancellationToken forwarding
    [Fact]
    public async Task Query_ShouldForwardCancellationToken()
    {
        var repository = new RecordingAdvertisementRepository();

        var service = new AdvertisementService(repository);

        using var cancellation =
            new CancellationTokenSource();

        var request = new AdvertisementActiveQueryRequest();

        await service.GetActiveByQueryAsync(
            request,
            Now,
            cancellation.Token);

        Assert.Equal(1, repository.ReadCount);

        Assert.Equal(
            cancellation.Token,
            repository.LastCancellationToken);
    }

    // Existing advertisement test data helper
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

    private static AdvertisementService CreateService(
        params AdvertisementDto[] ads)
    {
        return new AdvertisementService(
            new FakeAdvertisementRepository(ads));
    }

    // Existing fake repository
    private sealed class FakeAdvertisementRepository
        : IAdvertisementRepository
    {
        private readonly IReadOnlyList<AdvertisementDto> _ads;

        public FakeAdvertisementRepository(
            IReadOnlyList<AdvertisementDto> ads)
        {
            _ads = ads;
        }

        public Task<IReadOnlyList<AdvertisementDto>> GetActiveAsync(
            DateTimeOffset nowUtc,
            AdvertisementPlacement? placement = null,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<AdvertisementDto> result = _ads
                .Where(ad =>
                    ad.IsEnabled &&
                    ad.Status == AdvertisementStatus.Active &&
                    ad.StartAtUtc <= nowUtc &&
                    nowUtc < ad.EndAtUtc &&
                    (!placement.HasValue || ad.Placement == placement.Value))
                .ToArray();

            return Task.FromResult(result);
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

    // New recording repository for safety checks
    private sealed class RecordingAdvertisementRepository
        : IAdvertisementRepository
    {
        public int ReadCount { get; private set; }

        public CancellationToken LastCancellationToken
        {
            get;
            private set;
        }

        public Task<IReadOnlyList<AdvertisementDto>> GetActiveAsync(
            DateTimeOffset nowUtc,
            AdvertisementPlacement? placement = null,
            CancellationToken cancellationToken = default)
        {
            ReadCount++;
            LastCancellationToken = cancellationToken;
            IReadOnlyList<AdvertisementDto> advertisements =
                Array.Empty<AdvertisementDto>();
            return Task.FromResult(advertisements);
        }

        public Task<IReadOnlyList<AdvertisementDto>> GetAllAsync(
            CancellationToken cancellationToken = default)
        {
            ReadCount++;
            LastCancellationToken = cancellationToken;

            IReadOnlyList<AdvertisementDto> advertisements =
                Array.Empty<AdvertisementDto>();

            return Task.FromResult(advertisements);
        }

        public Task<AdvertisementDto?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<AdvertisementDto?>(null);
        }
    }
}
