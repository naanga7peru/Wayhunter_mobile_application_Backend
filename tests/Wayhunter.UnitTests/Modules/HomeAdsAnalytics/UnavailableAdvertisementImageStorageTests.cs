using Wayhunter.Api.Services.HomeAdsAnalytics;
using Wayhunter.Application.Modules.HomeAdsAnalytics.Interfaces;
using Xunit;

namespace Wayhunter.UnitTests.Modules.HomeAdsAnalytics;

public sealed class UnavailableAdvertisementImageStorageTests
{
    [Fact]
    public async Task Upload_WhenDisabled_FailsClosed()
    {
        var storage = new UnavailableAdvertisementImageStorage();

        var image = new AdvertisementValidatedImage(
            [137, 80, 78, 71],
            "image/png",
            ".png");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => storage.UploadAsync(Guid.NewGuid(), image));
    }

    [Fact]
    public async Task Delete_WhenDisabled_ReturnsFalse()
    {
        var storage = new UnavailableAdvertisementImageStorage();

        var deleted = await storage.TryDeleteOwnedAssetAsync(
            "wayhunter/m7/test-asset");

        Assert.False(deleted);
    }
}
