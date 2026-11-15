
using System;
using Wayhunter.Application.Modules.HomeAdsAnalytics.Validators;
using Xunit;

namespace Wayhunter.UnitTests.Modules.HomeAdsAnalytics;

public sealed class AdvertisementUrlValidatorTests
{
    // TEST 01 - Valid HTTPS image
    [Fact]
    public void ValidHttpsImageUrl_ShouldBeAccepted()
    {
        Assert.True(
            AdvertisementUrlValidator.IsValidImageUrl(
                "https://example.com/banner.jpg"));
    }

    // TEST 02 - Existing unsafe URL protection
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
        Assert.False(
            AdvertisementUrlValidator.IsValidImageUrl(url));
    }

    // TEST 03 - Empty image
    [Fact]
    public void EmptyImageUrl_ShouldBeRejected()
    {
        Assert.False(
            AdvertisementUrlValidator.IsValidImageUrl(""));
    }

    // TEST 04 - Optional destination
    [Fact]
    public void MissingDestinationUrl_ShouldBeAccepted()
    {
        Assert.True(
            AdvertisementUrlValidator.IsValidDestinationUrl(null));
    }

    // TEST 05 - Valid HTTPS destination
    [Fact]
    public void ValidHttpsDestinationUrl_ShouldBeAccepted()
    {
        Assert.True(
            AdvertisementUrlValidator.IsValidDestinationUrl(
                "https://example.com/travel"));
    }

    // TEST 06 - HTTP destination rejected
    [Fact]
    public void HttpDestinationUrl_ShouldBeRejected()
    {
        Assert.False(
            AdvertisementUrlValidator.IsValidDestinationUrl(
                "http://example.com/travel"));
    }

    // TEST 07 - URL length limit
    [Fact]
    public void TooLongImageUrl_ShouldBeRejected()
    {
        string url =
            "https://example.com/" + new string('a', 2100);

        Assert.False(
            AdvertisementUrlValidator.IsValidImageUrl(url));
    }

    // TEST 08 - Private / reserved IP address URLs
    [Theory]
    [InlineData("https://10.0.0.1/banner.jpg")]
    [InlineData("https://192.168.1.10/banner.jpg")]
    [InlineData("https://172.16.0.1/banner.jpg")]
    [InlineData("https://169.254.169.254/")]
    [InlineData("https://[::1]/banner.jpg")]
    [InlineData("https://[fc00::1]/banner.jpg")]
    public void IpAddressImageUrl_ShouldBeRejected(string url)
    {
        Assert.False(
            AdvertisementUrlValidator.IsValidImageUrl(url));
    }

    // TEST 09 - Invalid URL formats
    [Theory]
    [InlineData("not-a-url")]
    [InlineData("example.com/banner.jpg")]
    [InlineData("/banner.jpg")]
    [InlineData("ftp://example.com/banner.jpg")]
    [InlineData("data:image/png;base64,AAAA")]
    [InlineData("https://intranet/banner.jpg")]
    [InlineData("https://example.com./banner.jpg")]
    public void InvalidImageUrlFormat_ShouldBeRejected(string url)
    {
        Assert.False(
            AdvertisementUrlValidator.IsValidImageUrl(url));
    }

    // TEST 10 - Local/private hostname suffixes
    [Theory]
    [InlineData("https://test.local/banner.jpg")]
    [InlineData("https://test.internal/banner.jpg")]
    [InlineData("https://test.localhost/banner.jpg")]
    [InlineData("https://sub.test.internal/banner.jpg")]
    public void InternalHostname_ShouldBeRejected(string url)
    {
        Assert.False(
            AdvertisementUrlValidator.IsValidImageUrl(url));
    }

    // TEST 11 - Whitespace and control characters
    [Theory]
    [InlineData(" https://example.com/banner.jpg")]
    [InlineData("https://example.com/banner.jpg ")]
    [InlineData("https://example.com/\nimage.jpg")]
    [InlineData("https://example.com/\timage.jpg")]
    [InlineData("https://example.com\\banner.jpg")]
    public void UnsafeCharacters_ShouldBeRejected(string url)
    {
        Assert.False(
            AdvertisementUrlValidator.IsValidImageUrl(url));
    }

    // TEST 12 - Empty/whitespace destination is invalid.
    // Only null is accepted as a missing destination.
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public void BlankDestination_ShouldBeRejected(string url)
    {
        Assert.False(
            AdvertisementUrlValidator.IsValidDestinationUrl(url));
    }

    // TEST 13 - Destination must use the same security rules
    [Theory]
    [InlineData("https://127.0.0.1/travel")]
    [InlineData("https://192.168.1.1/travel")]
    [InlineData("https://[::1]/travel")]
    [InlineData("https://user:pass@example.com/travel")]
    [InlineData("https://example.com:8443/travel")]
    [InlineData("https://test.internal/travel")]
    [InlineData("javascript:alert(1)")]
    public void UnsafeDestination_ShouldBeRejected(string url)
    {
        Assert.False(
            AdvertisementUrlValidator.IsValidDestinationUrl(url));
    }

    // TEST 14 - Oversized destination URL
    [Fact]
    public void TooLongDestination_ShouldBeRejected()
    {
        string url =
            "https://example.com/" + new string('a', 2100);

        Assert.False(
            AdvertisementUrlValidator.IsValidDestinationUrl(url));
    }

    // TEST 15 - Valid public DNS subdomain
    [Fact]
    public void ValidPublicSubdomain_ShouldBeAccepted()
    {
        Assert.True(
            AdvertisementUrlValidator.IsValidImageUrl(
                "https://cdn.example.com/images/banner.jpg"));
    }
}
