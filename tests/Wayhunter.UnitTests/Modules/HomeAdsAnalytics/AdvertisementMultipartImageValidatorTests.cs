using Microsoft.AspNetCore.Http;
using SkiaSharp;
using Wayhunter.Api.Services.HomeAdsAnalytics;
using Xunit;

namespace Wayhunter.UnitTests.Modules.HomeAdsAnalytics;

public sealed class AdvertisementMultipartImageValidatorTests
{
    [Fact]
    public async Task ValidSingleImage_IsAccepted()
    {
        using var bitmap = new SKBitmap(16, 16);
        bitmap.Erase(SKColors.Blue);

        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);

        Assert.NotNull(encoded);

        using var stream = new MemoryStream(encoded!.ToArray());

        var file = CreateFile(stream, "file", "image/png");

        var files = new FormFileCollection { file };

        var form = new FormCollection(
            new Dictionary<string,
                Microsoft.Extensions.Primitives.StringValues>(),
            files);

        var result =
            await AdvertisementMultipartImageValidator.ValidateAsync(form);

        Assert.Equal("image/png", result.ContentType);
        Assert.Equal(".png", result.Extension);
        Assert.NotEmpty(result.Content);
    }

    [Fact]
    public async Task MultipleFiles_AreRejected()
    {
        using var firstStream = new MemoryStream([1]);
        using var secondStream = new MemoryStream([2]);

        var files = new FormFileCollection
        {
            CreateFile(firstStream, "file", "image/png"),
            CreateFile(secondStream, "file", "image/png")
        };

        var form = CreateForm(files);

        await Assert.ThrowsAsync<ArgumentException>(
            () => AdvertisementMultipartImageValidator.ValidateAsync(form));
    }

    [Fact]
    public async Task WrongFieldName_IsRejected()
    {
        using var stream = new MemoryStream([1]);

        var form = CreateForm(
            new FormFileCollection
            {
                CreateFile(stream, "image", "image/png")
            });

        await Assert.ThrowsAsync<ArgumentException>(
            () => AdvertisementMultipartImageValidator.ValidateAsync(form));
    }

    [Fact]
    public async Task OversizedFile_IsRejected()
    {
        using var stream = new MemoryStream(
            new byte[AdvertisementImageValidator.MaxBytes + 1]);

        var form = CreateForm(
            new FormFileCollection
            {
                CreateFile(stream, "file", "image/png")
            });

        await Assert.ThrowsAsync<ArgumentException>(
            () => AdvertisementMultipartImageValidator.ValidateAsync(form));
    }

    private static IFormFile CreateFile(
        Stream stream,
        string name,
        string contentType)
    {
        return new FormFile(
            stream,
            0,
            stream.Length,
            name,
            "untrusted-name.png")
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }

    private static IFormCollection CreateForm(
        IFormFileCollection files)
    {
        return new FormCollection(
            new Dictionary<string,
                Microsoft.Extensions.Primitives.StringValues>(),
            files);
    }
}
