
using System;
using Wayhunter.Domain.Modules.HomeAdsAnalytics.Entities;
using Xunit;

namespace Wayhunter.UnitTests.Modules.HomeAdsAnalytics;

public sealed class AdvertisementDomainTests
{
    private static readonly DateTimeOffset Start =
        new(2026, 11, 13, 10, 0, 0, TimeSpan.Zero);

    private static Advertisement CreateAd()
    {
        return new Advertisement(
            Guid.NewGuid(),
            "Wayhunter Ad",
            "https://example.com/ad.jpg",
            null,
            Start,
            Start.AddDays(1));
    }

    [Fact]
    public void NewAdvertisement_ShouldBeDisabled()
    {
        var ad = CreateAd();

        Assert.False(ad.IsEnabled);
    }

    [Fact]
    public void Enable_ShouldActivateAdvertisement()
    {
        var ad = CreateAd();

        ad.Enable();

        Assert.True(ad.IsEnabled);
    }

    [Fact]
    public void Disable_ShouldDeactivateAdvertisement()
    {
        var ad = CreateAd();

        ad.Enable();
        ad.Disable();

        Assert.False(ad.IsEnabled);
    }

    [Fact]
    public void EmptyId_ShouldThrowException()
    {
        Assert.Throws<ArgumentException>(() =>
            new Advertisement(
                Guid.Empty,
                "Wayhunter Ad",
                "https://example.com/ad.jpg",
                null,
                Start,
                Start.AddDays(1)));
    }

    [Fact]
    public void EmptyTitle_ShouldThrowException()
    {
        Assert.Throws<ArgumentException>(() =>
            new Advertisement(
                Guid.NewGuid(),
                "",
                "https://example.com/ad.jpg",
                null,
                Start,
                Start.AddDays(1)));
    }

    [Fact]
    public void InvalidDateRange_ShouldThrowException()
    {
        Assert.Throws<ArgumentException>(() =>
            new Advertisement(
                Guid.NewGuid(),
                "Wayhunter Ad",
                "https://example.com/ad.jpg",
                null,
                Start,
                Start.AddDays(-1)));
    }
}
