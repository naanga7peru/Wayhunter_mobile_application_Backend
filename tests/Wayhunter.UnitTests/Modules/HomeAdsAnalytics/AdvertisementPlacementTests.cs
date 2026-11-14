
using System;
using Wayhunter.Domain.Modules.HomeAdsAnalytics.Entities;
using Wayhunter.Domain.Modules.HomeAdsAnalytics.Enums;
using Xunit;

namespace Wayhunter.UnitTests.Modules.HomeAdsAnalytics;

public sealed class AdvertisementPlacementTests
{
    private static Advertisement CreateAdvertisement()
    {
        var start = new DateTimeOffset(
            2026, 11, 14, 10, 0, 0, TimeSpan.Zero);

        return new Advertisement(
            Guid.NewGuid(),
            "Wayhunter Advertisement",
            "https://example.com/banner.jpg",
            null,
            start,
            start.AddDays(1));
    }

    [Fact]
    public void NewAdvertisement_ShouldDefaultToHomeBanner()
    {
        var ad = CreateAdvertisement();

        Assert.Equal(
            AdvertisementPlacement.HomeBanner,
            ad.Placement);
    }

    [Fact]
    public void SetPlacement_ShouldAllowSponsoredPoi()
    {
        var ad = CreateAdvertisement();

        ad.SetPlacement(AdvertisementPlacement.SponsoredPoi);

        Assert.Equal(
            AdvertisementPlacement.SponsoredPoi,
            ad.Placement);
    }

    [Fact]
    public void SetPlacement_ShouldRejectUnspecified()
    {
        var ad = CreateAdvertisement();

        Assert.Throws<ArgumentException>(() =>
            ad.SetPlacement(
                AdvertisementPlacement.Unspecified));

        Assert.Equal(
            AdvertisementPlacement.HomeBanner,
            ad.Placement);
    }

    [Fact]
    public void SetPlacement_ShouldRejectUnknownValue()
    {
        var ad = CreateAdvertisement();

        Assert.Throws<ArgumentException>(() =>
            ad.SetPlacement((AdvertisementPlacement)999));

        Assert.Equal(
            AdvertisementPlacement.HomeBanner,
            ad.Placement);
    }
}
