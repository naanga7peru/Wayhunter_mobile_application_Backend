
using System;
using Wayhunter.Application.Modules.HomeAdsAnalytics.Dtos;
using Wayhunter.Application.Modules.HomeAdsAnalytics.Validators;
using Wayhunter.Domain.Modules.HomeAdsAnalytics.Enums;
using Xunit;

namespace Wayhunter.UnitTests.Modules.HomeAdsAnalytics;

public sealed class AdvertisementCreateRequestValidatorTests
{
    private static AdvertisementCreateRequest CreateValidRequest()
    {
        var start = new DateTimeOffset(
            2026, 11, 14, 10, 0, 0, TimeSpan.Zero);

        return new AdvertisementCreateRequest
        {
            Title = "Wayhunter Advertisement",
            ImageUrl = "https://example.com/banner.jpg",
            DestinationUrl = "https://example.com/travel",
            StartAtUtc = start,
            EndAtUtc = start.AddDays(7),
            Placement = AdvertisementPlacement.HomeBanner
        };
    }

    [Fact]
    public void ValidRequest_ShouldBeAccepted()
    {
        var request = CreateValidRequest();

        Assert.True(
            AdvertisementCreateRequestValidator.IsValid(request));
    }

    [Fact]
    public void NullRequest_ShouldBeRejected()
    {
        Assert.False(
            AdvertisementCreateRequestValidator.IsValid(null));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyTitle_ShouldBeRejected(string title)
    {
        var request = CreateValidRequest() with
        {
            Title = title
        };

        Assert.False(
            AdvertisementCreateRequestValidator.IsValid(request));
    }

    [Fact]
    public void LongTitle_ShouldBeRejected()
    {
        var request = CreateValidRequest() with
        {
            Title = new string('A', 201)
        };

        Assert.False(
            AdvertisementCreateRequestValidator.IsValid(request));
    }

    [Fact]
    public void UnsafeImageUrl_ShouldBeRejected()
    {
        var request = CreateValidRequest() with
        {
            ImageUrl = "http://example.com/banner.jpg"
        };

        Assert.False(
            AdvertisementCreateRequestValidator.IsValid(request));
    }

    [Fact]
    public void UnsafeDestinationUrl_ShouldBeRejected()
    {
        var request = CreateValidRequest() with
        {
            DestinationUrl = "javascript:alert(1)"
        };

        Assert.False(
            AdvertisementCreateRequestValidator.IsValid(request));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void InvalidDateRange_ShouldBeRejected(int minutes)
    {
        var original = CreateValidRequest();

        var request = original with
        {
            EndAtUtc = original.StartAtUtc.AddMinutes(minutes)
        };

        Assert.False(
            AdvertisementCreateRequestValidator.IsValid(request));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(999)]
    public void InvalidPlacement_ShouldBeRejected(int placement)
    {
        var request = CreateValidRequest() with
        {
            Placement = (AdvertisementPlacement)placement
        };

        Assert.False(
            AdvertisementCreateRequestValidator.IsValid(request));
    }
}
