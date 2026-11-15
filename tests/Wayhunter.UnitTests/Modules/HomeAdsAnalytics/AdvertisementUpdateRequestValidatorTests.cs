
using System;
using Wayhunter.Application.Modules.HomeAdsAnalytics.Dtos;
using Wayhunter.Application.Modules.HomeAdsAnalytics.Validators;
using Wayhunter.Domain.Modules.HomeAdsAnalytics.Enums;
using Xunit;

namespace Wayhunter.UnitTests.Modules.HomeAdsAnalytics;

public sealed class AdvertisementUpdateRequestValidatorTests
{
    private static AdvertisementUpdateRequest CreateValidRequest()
    {
        var start = new DateTimeOffset(
            2026, 11, 15, 10, 0, 0, TimeSpan.Zero);

        return new AdvertisementUpdateRequest
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
    public void ValidUpdateRequest_ShouldBeAccepted()
    {
        var request = CreateValidRequest();

        Assert.True(
            AdvertisementUpdateRequestValidator.IsValid(request));
    }

    [Fact]
    public void NullUpdateRequest_ShouldBeRejected()
    {
        Assert.False(
            AdvertisementUpdateRequestValidator.IsValid(null));
    }

    [Fact]
    public void MissingDestinationUrl_ShouldBeAccepted()
    {
        var request = CreateValidRequest() with
        {
            DestinationUrl = null
        };

        Assert.True(
            AdvertisementUpdateRequestValidator.IsValid(request));
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
            AdvertisementUpdateRequestValidator.IsValid(request));
    }

    [Fact]
    public void LongTitle_ShouldBeRejected()
    {
        var request = CreateValidRequest() with
        {
            Title = new string('A', 201)
        };

        Assert.False(
            AdvertisementUpdateRequestValidator.IsValid(request));
    }

    [Fact]
    public void UnsafeImageUrl_ShouldBeRejected()
    {
        var request = CreateValidRequest() with
        {
            ImageUrl = "http://example.com/banner.jpg"
        };

        Assert.False(
            AdvertisementUpdateRequestValidator.IsValid(request));
    }

    [Fact]
    public void UnsafeDestinationUrl_ShouldBeRejected()
    {
        var request = CreateValidRequest() with
        {
            DestinationUrl = "javascript:alert(1)"
        };

        Assert.False(
            AdvertisementUpdateRequestValidator.IsValid(request));
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
            AdvertisementUpdateRequestValidator.IsValid(request));
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
            AdvertisementUpdateRequestValidator.IsValid(request));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void MissingUtcDate_ShouldBeRejected(
        bool missingStart)
    {
        var original = CreateValidRequest();

        var request = missingStart
            ? original with { StartAtUtc = default }
            : original with { EndAtUtc = default };

        Assert.False(
            AdvertisementUpdateRequestValidator.IsValid(request));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void NonUtcOffset_ShouldBeRejected(
        bool invalidStart)
    {
        var original = CreateValidRequest();

        var offset = TimeSpan.FromMinutes(330);

        var request = invalidStart
            ? original with
            {
                StartAtUtc = original.StartAtUtc.ToOffset(offset)
            }
            : original with
            {
                EndAtUtc = original.EndAtUtc.ToOffset(offset)
            };

        Assert.False(
            AdvertisementUpdateRequestValidator.IsValid(request));
    }

}
