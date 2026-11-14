
using System;
using Wayhunter.Domain.Modules.HomeAdsAnalytics.Entities;
using Xunit;

namespace Wayhunter.UnitTests.Modules.HomeAdsAnalytics;

public sealed class AdvertisementCounterTests
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
    public void NewAdvertisement_ShouldHaveZeroCounters()
    {
        var ad = CreateAdvertisement();

        Assert.Equal(0L, ad.ImpressionCount);
        Assert.Equal(0L, ad.ClickCount);
    }

    [Fact]
    public void RecordImpression_ShouldIncreaseImpressionCount()
    {
        var ad = CreateAdvertisement();

        ad.RecordImpression();

        Assert.Equal(1L, ad.ImpressionCount);
        Assert.Equal(0L, ad.ClickCount);
    }

    [Fact]
    public void RecordClick_ShouldIncreaseClickCount()
    {
        var ad = CreateAdvertisement();

        ad.RecordClick();

        Assert.Equal(1L, ad.ClickCount);
        Assert.Equal(0L, ad.ImpressionCount);
    }

    [Fact]
    public void MultipleEvents_ShouldUpdateCountersIndependently()
    {
        var ad = CreateAdvertisement();

        ad.RecordImpression();
        ad.RecordImpression();
        ad.RecordImpression();

        ad.RecordClick();
        ad.RecordClick();

        Assert.Equal(3L, ad.ImpressionCount);
        Assert.Equal(2L, ad.ClickCount);
    }
}
