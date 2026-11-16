
using System;
using Wayhunter.Application.Modules.HomeAdsAnalytics.Dtos;
using Wayhunter.Application.Modules.HomeAdsAnalytics.Validators;
using Wayhunter.Domain.Modules.HomeAdsAnalytics.Enums;
using Xunit;

namespace Wayhunter.UnitTests.Modules.HomeAdsAnalytics;

public sealed class AdvertisementRequestParityRegressionTests
{
    private static AdvertisementCreateRequest ValidCreate()
    {
        var start = new DateTimeOffset(
            2026, 11, 16, 10, 0, 0, TimeSpan.Zero);

        return new AdvertisementCreateRequest
        {
            Title = "Wayhunter Advertisement",
            ImageUrl = "https://example.com/banner.jpg",
            DestinationUrl = null,
            StartAtUtc = start,
            EndAtUtc = start.AddDays(7),
            Placement = AdvertisementPlacement.HomeBanner
        };
    }

    private static AdvertisementUpdateRequest ToUpdate(
        AdvertisementCreateRequest request)
    {
        return new AdvertisementUpdateRequest
        {
            Title = request.Title,
            ImageUrl = request.ImageUrl,
            DestinationUrl = request.DestinationUrl,
            StartAtUtc = request.StartAtUtc,
            EndAtUtc = request.EndAtUtc,
            Placement = request.Placement
        };
    }

    // TEST 01 - Maximum allowed title length
    [Fact]
    public void Exact200CharacterTitle_ShouldBeAccepted()
    {
        var request = ValidCreate() with
        {
            Title = new string('A', 200)
        };

        Assert.True(
            AdvertisementCreateRequestValidator.IsValid(request));

        Assert.True(
            AdvertisementUpdateRequestValidator.IsValid(
                ToUpdate(request)));
    }

    // TEST 02 - Title exceeds maximum length
    [Fact]
    public void TitleOver200Characters_ShouldBeRejectedByBoth()
    {
        var request = ValidCreate() with
        {
            Title = new string('A', 201)
        };

        Assert.False(
            AdvertisementCreateRequestValidator.IsValid(request));

        Assert.False(
            AdvertisementUpdateRequestValidator.IsValid(
                ToUpdate(request)));
    }

    // TEST 03 - Missing image URL
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public void BlankImageUrl_ShouldBeRejectedByBoth(
        string imageUrl)
    {
        var request = ValidCreate() with
        {
            ImageUrl = imageUrl
        };

        Assert.False(
            AdvertisementCreateRequestValidator.IsValid(request));

        Assert.False(
            AdvertisementUpdateRequestValidator.IsValid(
                ToUpdate(request)));
    }

    // TEST 04 - Optional destination
    [Fact]
    public void NullDestination_ShouldBeAcceptedByBoth()
    {
        var request = ValidCreate() with
        {
            DestinationUrl = null
        };

        Assert.True(
            AdvertisementCreateRequestValidator.IsValid(request));

        Assert.True(
            AdvertisementUpdateRequestValidator.IsValid(
                ToUpdate(request)));
    }

    // TEST 05 - Unsafe destination
    [Theory]
    [InlineData("http://example.com")]
    [InlineData("javascript:alert(1)")]
    [InlineData("https://127.0.0.1/travel")]
    [InlineData("https://example.com:8443/travel")]
    public void UnsafeDestination_ShouldBeRejectedByBoth(
        string destinationUrl)
    {
        var request = ValidCreate() with
        {
            DestinationUrl = destinationUrl
        };

        Assert.False(
            AdvertisementCreateRequestValidator.IsValid(request));

        Assert.False(
            AdvertisementUpdateRequestValidator.IsValid(
                ToUpdate(request)));
    }

    // TEST 06 - Equal start and end timestamps
    [Fact]
    public void EqualDates_ShouldBeRejectedByBoth()
    {
        var original = ValidCreate();

        var request = original with
        {
            EndAtUtc = original.StartAtUtc
        };

        Assert.False(
            AdvertisementCreateRequestValidator.IsValid(request));

        Assert.False(
            AdvertisementUpdateRequestValidator.IsValid(
                ToUpdate(request)));
    }

    // TEST 07 - Non-UTC start
    [Fact]
    public void NonUtcStart_ShouldBeRejectedByBoth()
    {
        var original = ValidCreate();

        var request = original with
        {
            StartAtUtc = original.StartAtUtc.ToOffset(
                TimeSpan.FromHours(5.5))
        };

        Assert.False(
            AdvertisementCreateRequestValidator.IsValid(request));

        Assert.False(
            AdvertisementUpdateRequestValidator.IsValid(
                ToUpdate(request)));
    }

    // TEST 08 - Unsupported placements
    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(999)]
    public void InvalidPlacement_ShouldBeRejectedByBoth(
        int placement)
    {
        var request = ValidCreate() with
        {
            Placement = (AdvertisementPlacement)placement
        };

        Assert.False(
            AdvertisementCreateRequestValidator.IsValid(request));

        Assert.False(
            AdvertisementUpdateRequestValidator.IsValid(
                ToUpdate(request)));
    }

    // TEST 09 - Status update negative value
    [Fact]
    public void NegativeStatus_ShouldBeRejected()
    {
        var request = new AdvertisementStatusUpdateRequest
        {
            Status = (AdvertisementStatus)(-1)
        };

        Assert.False(
            AdvertisementStatusUpdateRequestValidator.IsValid(
                request));
    }

    // TEST 10 - Valid sponsored placement
    [Fact]
    public void SponsoredPoi_ShouldBeAcceptedByBoth()
    {
        var request = ValidCreate() with
        {
            Placement = AdvertisementPlacement.SponsoredPoi
        };

        Assert.True(
            AdvertisementCreateRequestValidator.IsValid(request));

        Assert.True(
            AdvertisementUpdateRequestValidator.IsValid(
                ToUpdate(request)));
    }
}
