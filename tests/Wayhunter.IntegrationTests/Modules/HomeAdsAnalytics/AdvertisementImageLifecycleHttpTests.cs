using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

using SkiaSharp;

using Wayhunter.Application.Modules.HomeAdsAnalytics.Dtos;
using Wayhunter.Application.Modules.HomeAdsAnalytics.Interfaces;

using Xunit;

namespace Wayhunter.IntegrationTests.Modules.HomeAdsAnalytics;

public sealed partial class AdvertisementAdminPositiveHttpTests
{
    // In-memory compare-and-swap implementation. No SQL.
    private sealed partial class FakeRepository :
        IAdvertisementImageUpdateRepository
    {
        public bool ForceImageConflict { get; set; }
        public bool SimulateSqlResponseLost { get; set; }

        public Task<AdvertisementDto?> UpdateImageUrlAsync(
            Guid advertisementId,
            string expectedImageUrl,
            string trustedHttpsUrl,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (SimulateSqlResponseLost)
            {
                // SQL may have committed before its response was lost.
                // Never infer a rollback from this exception.
                throw new InvalidOperationException(
                    "Simulated SQL response lost after possible commit.");
            }

            if (ForceImageConflict ||
                !_items.TryGetValue(advertisementId, out var current) ||
                !string.Equals(
                    current.ImageUrl,
                    expectedImageUrl,
                    StringComparison.Ordinal))
            {
                return Task.FromResult<AdvertisementDto?>(null);
            }

            var updated = current with
            {
                ImageUrl = trustedHttpsUrl
            };

            _items[advertisementId] = updated;

            return Task.FromResult<AdvertisementDto?>(updated);
        }
    }

    // In-memory provider. Never uses HTTP or real credentials.
    private sealed class FakeImageStorage : IAdvertisementImageStorage
    {
        public const string NewUrl =
            "https://example.com/replaced-banner.png";

        public int UploadCount { get; private set; }
        public int DeleteCount { get; private set; }

        public string? LastAssetId { get; private set; }

        public Task<AdvertisementStoredImage> UploadAsync(
            Guid advertisementId,
            AdvertisementValidatedImage image,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            UploadCount++;

            LastAssetId =
                $"wayhunter/advertisements/{advertisementId:D}/{Guid.NewGuid():N}";

            return Task.FromResult(
                new AdvertisementStoredImage(
                    LastAssetId,
                    NewUrl));
        }

        public Task<bool> TryDeleteOwnedAssetAsync(
            string assetId,
            CancellationToken cancellationToken = default)
        {
            DeleteCount++;

            return Task.FromResult(
                string.Equals(
                    assetId,
                    LastAssetId,
                    StringComparison.Ordinal));
        }
    }

    private static MultipartFormDataContent ValidImageForm()
    {
        using var bitmap = new SKBitmap(16, 16);
        bitmap.Erase(SKColors.Blue);

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(
            SKEncodedImageFormat.Png,
            100);

        Assert.NotNull(data);

        var form = new MultipartFormDataContent();

        var file = new ByteArrayContent(data!.ToArray());

        file.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue(
                "image/png");

        form.Add(file, "file", "test.png");

        return form;
    }

    [Fact]
    public async Task Admin_ImageUpload_FakeLifecycle_Returns200()
    {
        await using var server = await CreateServerAsync(
            enableImageUpload: true,
            enableLifecycle: true);

        var repository = server.Services
            .GetRequiredService<FakeRepository>();

        var storage = server.Services
            .GetRequiredService<FakeImageStorage>();

        var advertisement = await repository.CreateAsync(
            ValidCreate());

        using var client = server.GetTestClient();

        client.DefaultRequestHeaders.Add(
            "X-Test-User",
            "admin");

        using var form = ValidImageForm();

        using var response = await client.PostAsync(
            $"{Route}/{advertisement.Id}/image",
            form);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = await response.Content
            .ReadFromJsonAsync<AdvertisementDto>();

        Assert.NotNull(updated);
        Assert.Equal(advertisement.Id, updated.Id);
        Assert.Equal(FakeImageStorage.NewUrl, updated.ImageUrl);

        var persisted = await repository.GetByIdAsync(
            advertisement.Id);

        Assert.NotNull(persisted);
        Assert.Equal(
            FakeImageStorage.NewUrl,
            persisted.ImageUrl);

        Assert.Equal(1, storage.UploadCount);
        Assert.Equal(0, storage.DeleteCount);
    }

    [Fact]
    public async Task Admin_ImageUpload_AmbiguousSqlFailure_DoesNotDeleteAsset()
    {
        await using var server = await CreateServerAsync(
            enableImageUpload: true,
            enableLifecycle: true);

        var repository = server.Services
            .GetRequiredService<FakeRepository>();

        var storage = server.Services
            .GetRequiredService<FakeImageStorage>();

        var advertisement = await repository.CreateAsync(
            ValidCreate());

        repository.SimulateSqlResponseLost = true;

        using var client = server.GetTestClient();

        client.DefaultRequestHeaders.Add(
            "X-Test-User",
            "admin");

        using var form = ValidImageForm();

        using var response = await client.PostAsync(
            $"{Route}/{advertisement.Id}/image",
            form);

        Assert.Equal(
            HttpStatusCode.InternalServerError,
            response.StatusCode);

        Assert.Equal(1, storage.UploadCount);

        // The upload succeeded before the simulated SQL exception.
        Assert.NotNull(storage.LastAssetId);

        // CRITICAL: SQL may have committed. Do not delete the asset.
        Assert.Equal(0, storage.DeleteCount);

        // The fake SQL repository throws before changing its in-memory
        // state. This is NOT proof that a real SQL operation rolled back.
        var persisted = await repository.GetByIdAsync(
            advertisement.Id);

        Assert.NotNull(persisted);
        Assert.Equal(
            advertisement.ImageUrl,
            persisted.ImageUrl);
    }
    [Fact]
    public async Task Admin_ImageUpload_CasConflict_Returns409()
    {
        await using var server = await CreateServerAsync(
            enableImageUpload: true,
            enableLifecycle: true);

        var repository = server.Services
            .GetRequiredService<FakeRepository>();

        var storage = server.Services
            .GetRequiredService<FakeImageStorage>();

        var advertisement = await repository.CreateAsync(
            ValidCreate());

        repository.ForceImageConflict = true;

        using var client = server.GetTestClient();

        client.DefaultRequestHeaders.Add(
            "X-Test-User",
            "admin");

        using var form = ValidImageForm();

        using var response = await client.PostAsync(
            $"{Route}/{advertisement.Id}/image",
            form);

        Assert.Equal(
            HttpStatusCode.Conflict,
            response.StatusCode);

        var persisted = await repository.GetByIdAsync(
            advertisement.Id);

        Assert.NotNull(persisted);
        Assert.Equal(
            advertisement.ImageUrl,
            persisted.ImageUrl);

        Assert.Equal(1, storage.UploadCount);
        Assert.Equal(0, storage.DeleteCount);
    }
}