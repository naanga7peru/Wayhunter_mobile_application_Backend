using SkiaSharp;
using Wayhunter.Application.Modules.HomeAdsAnalytics.Interfaces;

namespace Wayhunter.Api.Services.HomeAdsAnalytics;

public static class AdvertisementImageValidator
{
    public const int MaxBytes = 5 * 1024 * 1024;
    public const int MaxDimension = 4096;
    public const long MaxPixels = 16_000_000;

    public static AdvertisementValidatedImage Validate(
        byte[] data,
        string contentType)
    {
        if (data is null || data.Length == 0 ||
            data.Length > MaxBytes)
        {
            throw new ArgumentException("Invalid image size.");
        }

        var jpeg = data.Length >= 3 &&
                   data[0] == 0xFF &&
                   data[1] == 0xD8 &&
                   data[2] == 0xFF;

        var png = data.Length >= 8 &&
                  data.AsSpan(0, 8).SequenceEqual(
                      new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });

        var webp = data.Length >= 12 &&
                   data.AsSpan(0, 4).SequenceEqual("RIFF"u8) &&
                   data.AsSpan(8, 4).SequenceEqual("WEBP"u8);

        if (!(
            (jpeg && contentType == "image/jpeg") ||
            (png && contentType == "image/png") ||
            (webp && contentType == "image/webp")))
        {
            throw new ArgumentException(
                "Unsupported or mismatched image format.");
        }

        using var stream = new MemoryStream(
            data, writable: false);

        using var codec = SKCodec.Create(stream);

        if (codec is null)
        {
            throw new ArgumentException(
                "Image content cannot be decoded.");
        }

        var expectedFormat = jpeg
            ? SKEncodedImageFormat.Jpeg
            : png
                ? SKEncodedImageFormat.Png
                : SKEncodedImageFormat.Webp;

        if (codec.EncodedFormat != expectedFormat)
        {
            throw new ArgumentException(
                "Image content does not match its declared format.");
        }

        var info = codec.Info;

        if (info.Width <= 0 ||
            info.Height <= 0 ||
            info.Width > MaxDimension ||
            info.Height > MaxDimension ||
            (long)info.Width * info.Height > MaxPixels)
        {
            throw new ArgumentException(
                "Image dimensions exceed allowed limits.");
        }

        var decodeInfo = new SKImageInfo(
            info.Width,
            info.Height,
            SKColorType.Rgba8888,
            SKAlphaType.Premul);

        using var bitmap = new SKBitmap(decodeInfo);

        var decodeResult = codec.GetPixels(
            decodeInfo,
            bitmap.GetPixels());

        if (decodeResult != SKCodecResult.Success)
        {
            throw new ArgumentException(
                "Image decoding failed.");
        }

        using var image = SKImage.FromBitmap(bitmap);

        if (image is null)
        {
            throw new ArgumentException(
                "Unable to process image.");
        }

        // Re-encode pixels without carrying original metadata.
        using var encoded = image.Encode(
            SKEncodedImageFormat.Png,
            100);

        if (encoded is null ||
            encoded.Size == 0 ||
            encoded.Size > MaxBytes)
        {
            throw new ArgumentException(
                "Processed image exceeds allowed size.");
        }

        return new AdvertisementValidatedImage(
            encoded.ToArray(),
            "image/png",
            ".png");
    }
}
