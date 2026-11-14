
using System;
using Wayhunter.Application.Modules.HomeAdsAnalytics.Validators;
using Xunit;

namespace Wayhunter.UnitTests.Modules.HomeAdsAnalytics;


public sealed class AdvertisementUrlValidatorTests
{
    [Fact]
    public void ValidHttpsImageUrl_ShouldBeAccepted()
    {
        bool result =
            AdvertisementUrlValidator.IsValidImageUrl(
                "https://example.com/banner.jpg");

        Assert.True(result);
    }

    [Theory]
    [InlineData("http://example.com/banner.jpg")]
    [InlineData("javascript:alert(1)")]
    [InlineData("https://localhost/banner.jpg")]
    [InlineData("https://127.0.0.1/banner.jpg")]
    [InlineData("https://user:pass@example.com/banner.jpg")]
    [InlineData("https://example.com:8443/banner.jpg")]
    [InlineData("https://server.local/banner.jpg")]
    [InlineData("https://server.internal/banner.jpg")]
    public void UnsafeImageUrl_ShouldBeRejected(string url)
    {
        bool result =
            AdvertisementUrlValidator.IsValidImageUrl(url);

        Assert.False(result);
    }

    [Fact]
    public void EmptyImageUrl_ShouldBeRejected()
    {
        Assert.False(
            AdvertisementUrlValidator.IsValidImageUrl(""));
    }

    [Fact]
    public void MissingDestinationUrl_ShouldBeAccepted()
    {
        Assert.True(
            AdvertisementUrlValidator.IsValidDestinationUrl(null));
    }

    [Fact]
    public void ValidHttpsDestinationUrl_ShouldBeAccepted()
    {
        Assert.True(
            AdvertisementUrlValidator.IsValidDestinationUrl(
                "https://example.com/travel"));
    }

    [Fact]
    public void HttpDestinationUrl_ShouldBeRejected()
    {
        Assert.False(
            AdvertisementUrlValidator.IsValidDestinationUrl(
                "http://example.com/travel"));
    }

    [Fact]
    public void TooLongImageUrl_ShouldBeRejected()
    {
        string url =
            "https://example.com/" + new string('a', 2100);

        Assert.False(
            AdvertisementUrlValidator.IsValidImageUrl(url));
    }
}
