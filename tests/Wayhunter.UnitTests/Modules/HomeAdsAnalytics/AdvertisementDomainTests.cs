
using System;
using Wayhunter.Domain.Modules.HomeAdsAnalytics.Entities;
using Wayhunter.Domain.Modules.HomeAdsAnalytics.Enums;
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

    // Existing tests
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

    // New: Missing image URL
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public void MissingImageUrl_ShouldThrowException(string imageUrl)
    {
        Assert.Throws<ArgumentException>(() =>
            new Advertisement(
                Guid.NewGuid(),
                "Wayhunter Ad",
                imageUrl,
                null,
                Start,
                Start.AddDays(1)));
    }

    // New: Equal date window
    [Fact]
    public void EqualStartAndEnd_ShouldThrowException()
    {
        Assert.Throws<ArgumentException>(() =>
            new Advertisement(
                Guid.NewGuid(),
                "Wayhunter Ad",
                "https://example.com/ad.jpg",
                null,
                Start,
                Start));
    }

    // New: Title trimming
    [Fact]
    public void Title_ShouldBeTrimmed()
    {
        var ad = new Advertisement(
            Guid.NewGuid(),
            "  Wayhunter Ad  ",
            "https://example.com/ad.jpg",
            null,
            Start,
            Start.AddDays(1));

        Assert.Equal("Wayhunter Ad", ad.Title);
    }

    // New: Image URL trimming
    [Fact]
    public void ImageUrl_ShouldBeTrimmed()
    {
        var ad = new Advertisement(
            Guid.NewGuid(),
            "Wayhunter Ad",
            "  https://example.com/ad.jpg  ",
            null,
            Start,
            Start.AddDays(1));

        Assert.Equal(
            "https://example.com/ad.jpg",
            ad.ImageUrl);
    }

    // New: Default placement
    [Fact]
    public void NewAdvertisement_ShouldDefaultToHomeBanner()
    {
        var ad = CreateAd();

        Assert.Equal(
            AdvertisementPlacement.HomeBanner,
            ad.Placement);
    }

    // New: Valid placement
    [Fact]
    public void SetPlacement_ShouldAcceptSponsoredPoi()
    {
        var ad = CreateAd();

        ad.SetPlacement(
            AdvertisementPlacement.SponsoredPoi);

        Assert.Equal(
            AdvertisementPlacement.SponsoredPoi,
            ad.Placement);
    }

    // New: Invalid placement
    [Theory]
    [InlineData(0)]
    [InlineData(999)]
    [InlineData(-1)]
    public void InvalidPlacement_ShouldThrowException(int placement)
    {
        var ad = CreateAd();

        Assert.Throws<ArgumentException>(() =>
            ad.SetPlacement(
                (AdvertisementPlacement)placement));

        Assert.Equal(
            AdvertisementPlacement.HomeBanner,
            ad.Placement);
    }

    // New: Destination URL retained
    [Fact]
    public void DestinationUrl_ShouldBeRetained()
    {
        var destination =
            "https://example.com/travel";

        var ad = new Advertisement(
            Guid.NewGuid(),
            "Wayhunter Ad",
            "https://example.com/ad.jpg",
            destination,
            Start,
            Start.AddDays(1));

        Assert.Equal(destination, ad.DestinationUrl);
    }
}
