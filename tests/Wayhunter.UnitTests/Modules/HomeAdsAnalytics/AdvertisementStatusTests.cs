
using System;
using Wayhunter.Domain.Modules.HomeAdsAnalytics.Entities;
using Wayhunter.Domain.Modules.HomeAdsAnalytics.Enums;
using Xunit;

namespace Wayhunter.UnitTests.Modules.HomeAdsAnalytics;

public sealed class AdvertisementStatusTests
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
    public void NewAdvertisement_ShouldHaveDraftStatus()
    {
        var ad = CreateAdvertisement();

        Assert.Equal(AdvertisementStatus.Draft, ad.Status);
        Assert.False(ad.IsEnabled);
    }

    [Fact]
    public void Enable_ShouldSetActiveStatus()
    {
        var ad = CreateAdvertisement();

        ad.Enable();

        Assert.Equal(AdvertisementStatus.Active, ad.Status);
        Assert.True(ad.IsEnabled);
    }

    [Fact]
    public void Disable_ShouldSetPausedStatus()
    {
        var ad = CreateAdvertisement();

        ad.Enable();
        ad.Disable();

        Assert.Equal(AdvertisementStatus.Paused, ad.Status);
        Assert.False(ad.IsEnabled);
    }

    [Fact]
    public void Archive_ShouldSetArchivedStatus()
    {
        var ad = CreateAdvertisement();

        ad.Enable();
        ad.Archive();

        Assert.Equal(AdvertisementStatus.Archived, ad.Status);
        Assert.False(ad.IsEnabled);
    }

    [Fact]
    public void ArchivedAdvertisement_CannotBeEnabled()
    {
        var ad = CreateAdvertisement();

        ad.Archive();

        Assert.Throws<InvalidOperationException>(
            () => ad.Enable());

        Assert.Equal(AdvertisementStatus.Archived, ad.Status);
        Assert.False(ad.IsEnabled);
    }
}
