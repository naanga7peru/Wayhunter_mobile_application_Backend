
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
    public void NonSriLankanDistrict_ShouldBeRejected(string district)
    {
        var request = new AdvertisementActiveQueryRequest
        {
            District = district
        };

        Assert.False(
            AdvertisementActiveQueryRequestValidator.IsValid(request));
    }

    // NEW - All optional filters together.
    [Fact]
    public void CombinedValidFilters_ShouldBeAccepted()
    {
        var request = new AdvertisementActiveQueryRequest
        {
            AtUtc = new DateTimeOffset(
                2026, 11, 16, 10, 0, 0, TimeSpan.Zero),
            Placement = AdvertisementPlacement.HomeBanner,
            District = "Jaffna"
        };

        Assert.True(
            AdvertisementActiveQueryRequestValidator.IsValid(request));
    }

    // NEW - An invalid district must reject the entire query.
    [Fact]
    public void CombinedFilters_InvalidDistrict_ShouldBeRejected()
    {
        var request = new AdvertisementActiveQueryRequest
        {
            AtUtc = new DateTimeOffset(
                2026, 11, 16, 10, 0, 0, TimeSpan.Zero),
            Placement = AdvertisementPlacement.SponsoredPoi,
            District = "Atlantis"
        };

        Assert.False(
            AdvertisementActiveQueryRequestValidator.IsValid(request));
    }

    // NEW - An invalid placement must reject the entire query.
    [Fact]
    public void CombinedFilters_InvalidPlacement_ShouldBeRejected()
    {
        var request = new AdvertisementActiveQueryRequest
        {
            AtUtc = new DateTimeOffset(
                2026, 11, 16, 10, 0, 0, TimeSpan.Zero),
            Placement = (AdvertisementPlacement)999,
            District = "Jaffna"
        };

        Assert.False(
            AdvertisementActiveQueryRequestValidator.IsValid(request));
    }

    // NEW - A non-UTC date must reject otherwise valid filters.
    [Fact]
    public void CombinedFilters_NonUtcDate_ShouldBeRejected()
    {
        var request = new AdvertisementActiveQueryRequest
        {
            AtUtc = new DateTimeOffset(
                2026, 11, 16, 10, 0, 0,
                TimeSpan.FromHours(5.5)),
            Placement = AdvertisementPlacement.HomeBanner,
            District = "Jaffna"
        };

        Assert.False(
            AdvertisementActiveQueryRequestValidator.IsValid(request));
    }

    // NEW - Valid non-default UTC boundary values.
    [Theory]
    [InlineData(2000, 1, 1)]
    [InlineData(2030, 12, 31)]
    public void ValidUtcBoundaryDates_ShouldBeAccepted(
        int year,
        int month,
        int day)
    {
        var request = new AdvertisementActiveQueryRequest
        {
            AtUtc = new DateTimeOffset(
                year, month, day, 0, 0, 0, TimeSpan.Zero)
        };

        Assert.True(
            AdvertisementActiveQueryRequestValidator.IsValid(request));
    }

    // NEW - Existing district matching is case-insensitive.
    [Theory]
    [InlineData("JAFFNA")]
    [InlineData("jaffna")]
    [InlineData("colombo")]
    public void ValidDistrictCaseVariants_ShouldBeAccepted(
        string district)
    {
        var request = new AdvertisementActiveQueryRequest
        {
            District = district
        };

        Assert.True(
            AdvertisementActiveQueryRequestValidator.IsValid(request));
    }

    // NEW - Whitespace must not be silently normalized.
    [Theory]
    [InlineData("Jaffna ")]
    [InlineData(" Jaffna")]
    [InlineData("\tJaffna")]
    [InlineData("Jaffna\n")]
    public void DistrictWithWhitespace_ShouldBeRejected(
        string district)
    {
        var request = new AdvertisementActiveQueryRequest
        {
            District = district
        };

        Assert.False(
            AdvertisementActiveQueryRequestValidator.IsValid(request));
    }
}
