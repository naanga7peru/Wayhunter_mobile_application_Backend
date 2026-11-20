using SkiaSharp;
using Wayhunter.Api.Services.HomeAdsAnalytics;
using Xunit;

namespace Wayhunter.UnitTests.Modules.HomeAdsAnalytics;

public sealed class AdvertisementImageValidatorTests
{
    [Theory]
    [InlineData(SKEncodedImageFormat.Png, "image/png")]
    [InlineData(SKEncodedImageFormat.Jpeg, "image/jpeg")]
    [InlineData(SKEncodedImageFormat.Webp, "image/webp")]
    public void ValidImage_IsDecodedAndReencodedAsPng(
        SKEncodedImageFormat format,
        string mime)
    {
        var bytes = CreateImage(format);

        var result = AdvertisementImageValidator.Validate(
            bytes, mime);

        Assert.Equal("image/png", result.ContentType);
        Assert.Equal(".png", result.Extension);
        Assert.NotEmpty(result.Content);

        using var decoded = SKBitmap.Decode(result.Content);

        Assert.NotNull(decoded);
        Assert.Equal(16, decoded!.Width);
        Assert.Equal(16, decoded.Height);
    }

    [Fact]
    public void EmptyFile_IsRejected()
    {
        Assert.Throws<ArgumentException>(() =>
            AdvertisementImageValidator.Validate(
                [], "image/png"));
    }

    [Fact]
    public void OversizedFile_IsRejected()
    {
        var bytes = new byte[
            AdvertisementImageValidator.MaxBytes + 1];

        Assert.Throws<ArgumentException>(() =>
            AdvertisementImageValidator.Validate(
                bytes, "image/png"));
    }

    [Theory]
    [InlineData("image/svg+xml")]
    [InlineData("image/gif")]
    [InlineData("image/heic")]
    [InlineData("application/pdf")]
    [InlineData("application/octet-stream")]
    public void UnsupportedMime_IsRejected(string mime)
    {
        var bytes = CreateImage(SKEncodedImageFormat.Png);

        Assert.Throws<ArgumentException>(() =>
            AdvertisementImageValidator.Validate(
                bytes, mime));
    }

    [Fact]
    public void SpoofedMime_IsRejected()
    {
        var bytes = CreateImage(SKEncodedImageFormat.Jpeg);

        Assert.Throws<ArgumentException>(() =>
            AdvertisementImageValidator.Validate(
                bytes, "image/png"));
    }

    [Fact]
    public void CorruptPng_IsRejected()
    {
        var bytes = new byte[]
        {
            137, 80, 78, 71, 13, 10, 26, 10,
            1, 2, 3, 4, 5, 6, 7, 8
        };

        Assert.Throws<ArgumentException>(() =>
            AdvertisementImageValidator.Validate(
                bytes, "image/png"));
    }

    [Fact]
    public void RandomContent_IsRejected()
    {
        var bytes = new byte[] { 1, 2, 3, 4, 5 };

        Assert.Throws<ArgumentException>(() =>
            AdvertisementImageValidator.Validate(
                bytes, "image/jpeg"));
    }

    private static byte[] CreateImage(
        SKEncodedImageFormat format)
    {
        using var bitmap = new SKBitmap(16, 16);
        bitmap.Erase(SKColors.Blue);

        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(format, 90);

        Assert.NotNull(encoded);

        return encoded!.ToArray();
    }
}
