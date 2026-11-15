
using System;
using Wayhunter.Application.Modules.HomeAdsAnalytics.Dtos;
using Wayhunter.Application.Modules.HomeAdsAnalytics.Validators;
using Wayhunter.Domain.Modules.HomeAdsAnalytics.Enums;
using Xunit;

namespace Wayhunter.UnitTests.Modules.HomeAdsAnalytics;

public sealed class AdvertisementActiveQueryRequestValidatorTests
{
    [Fact]
    public void NullRequest_ShouldBeRejected()
    {
        Assert.False(
            AdvertisementActiveQueryRequestValidator.IsValid(null));
    }

    [Fact]
    public void EmptyRequest_ShouldBeAccepted()
    {
        var request = new AdvertisementActiveQueryRequest();

        Assert.True(
            AdvertisementActiveQueryRequestValidator.IsValid(request));
    }

    [Fact]
    public void ValidUtcDate_ShouldBeAccepted()
    {
        var request = new AdvertisementActiveQueryRequest
        {
            AtUtc = new DateTimeOffset(
                2026, 11, 15, 10, 0, 0, TimeSpan.Zero)
        };

        Assert.True(
            AdvertisementActiveQueryRequestValidator.IsValid(request));
    }

    [Fact]
    public void NonUtcDate_ShouldBeRejected()
    {
        var request = new AdvertisementActiveQueryRequest
        {
            AtUtc = new DateTimeOffset(
                2026, 11, 15, 10, 0, 0,
                TimeSpan.FromHours(5.5))
        };

        Assert.False(
            AdvertisementActiveQueryRequestValidator.IsValid(request));
    }

    [Fact]
    public void DefaultDate_ShouldBeRejected()
    {
        var request = new AdvertisementActiveQueryRequest
        {
            AtUtc = default(DateTimeOffset)
        };

        Assert.False(
            AdvertisementActiveQueryRequestValidator.IsValid(request));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(999)]
    public void InvalidPlacement_ShouldBeRejected(int placement)
    {
        var request = new AdvertisementActiveQueryRequest
        {
            Placement = (AdvertisementPlacement)placement
        };

        Assert.False(
            AdvertisementActiveQueryRequestValidator.IsValid(request));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void ValidPlacement_ShouldBeAccepted(int placement)
    {
        var request = new AdvertisementActiveQueryRequest
        {
            Placement = (AdvertisementPlacement)placement
        };

        Assert.True(
            AdvertisementActiveQueryRequestValidator.IsValid(request));
    }

    [Fact]
    public void ValidDistrict_ShouldBeAccepted()
    {
        var request = new AdvertisementActiveQueryRequest
        {
            District = "Jaffna"
        };

        Assert.True(
            AdvertisementActiveQueryRequestValidator.IsValid(request));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(" Colombo")]
    [InlineData("Colombo ")]
    [InlineData("Colombo1")]
    [InlineData("Colombo!")]
    [InlineData("server.local")]
    public void InvalidDistrict_ShouldBeRejected(string district)
    {
        var request = new AdvertisementActiveQueryRequest
        {
            District = district
        };

        Assert.False(
            AdvertisementActiveQueryRequestValidator.IsValid(request));
    }

    [Fact]
    public void TooLongDistrict_ShouldBeRejected()
    {
        var request = new AdvertisementActiveQueryRequest
        {
            District = new string('A', 101)
        };

        Assert.False(
            AdvertisementActiveQueryRequestValidator.IsValid(request));
    }

    [Theory]
    [InlineData("London")]
    [InlineData("Paris")]
    [InlineData("Atlantis")]
    public void NonSriLankanDistrict_ShouldBeRejected(
        string district)
    {
        var request = new AdvertisementActiveQueryRequest
        {
            District = district
        };

        Assert.False(
            AdvertisementActiveQueryRequestValidator.IsValid(
                request));
    }

}
