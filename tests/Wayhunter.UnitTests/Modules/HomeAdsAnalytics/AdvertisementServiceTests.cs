
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.HomeAdsAnalytics.Dtos;
using Wayhunter.Application.Modules.HomeAdsAnalytics.Interfaces;
using Wayhunter.Application.Modules.HomeAdsAnalytics.Services;
using Xunit;

namespace Wayhunter.UnitTests.Modules.HomeAdsAnalytics;

public sealed class AdvertisementServiceTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 11, 13, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ActiveAd_IsReturned()
    {
        var ad = CreateAd();
        var result = await CreateService(ad).GetActiveAsync(Now);

        Assert.Single(result);
        Assert.Equal(ad.Id, result[0].Id);
    }

    [Fact]
    public async Task DisabledAd_IsExcluded()
    {
        var ad = CreateAd(enabled: false);
        var result = await CreateService(ad).GetActiveAsync(Now);

        Assert.Empty(result);
    }

    [Fact]
    public async Task FutureAd_IsExcluded()
    {
        var ad = CreateAd(startMinutes: 10, endMinutes: 20);
        var result = await CreateService(ad).GetActiveAsync(Now);

        Assert.Empty(result);
    }

    [Fact]
    public async Task ExpiredAd_IsExcluded()
    {
        var ad = CreateAd(startMinutes: -20, endMinutes: -10);
        var result = await CreateService(ad).GetActiveAsync(Now);

        Assert.Empty(result);
    }

    [Fact]
    public async Task AdAtExactExpiry_IsExcluded()
    {
        var ad = CreateAd(startMinutes: -10, endMinutes: 0);
        var result = await CreateService(ad).GetActiveAsync(Now);

        Assert.Empty(result);
    }

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

    private static AdvertisementService CreateService(
        params AdvertisementDto[] ads)
    {
        return new AdvertisementService(
            new FakeAdvertisementRepository(ads));
    }

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
