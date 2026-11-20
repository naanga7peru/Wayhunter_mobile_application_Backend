using Xunit;
using Wayhunter.Api.Services.HomeAdsAnalytics;

namespace Wayhunter.UnitTests.Modules.HomeAdsAnalytics;

public sealed class AdvertisementImageAssetPolicyTests
{
    [Fact]
    public void CreateAssetId_UsesAdvertisementOwnedNamespace()
    {
        var advertisementId = Guid.NewGuid();

        var assetId = AdvertisementImageAssetPolicy.CreateAssetId(
            advertisementId);

        Assert.StartsWith(
            $"wayhunter/advertisements/{advertisementId:D}/",
            assetId);

        Assert.True(
            AdvertisementImageAssetPolicy.IsOwnedByAdvertisement(
                advertisementId, assetId));
    }

    [Fact]
    public void CreateAssetId_GeneratesUniqueIdentifiers()
    {
        var advertisementId = Guid.NewGuid();

        var first = AdvertisementImageAssetPolicy.CreateAssetId(
            advertisementId);

        var second = AdvertisementImageAssetPolicy.CreateAssetId(
            advertisementId);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void ForeignAdvertisementAsset_IsRejected()
    {
        var firstAdvertisement = Guid.NewGuid();
        var secondAdvertisement = Guid.NewGuid();

        var assetId = AdvertisementImageAssetPolicy.CreateAssetId(
            firstAdvertisement);

        Assert.False(
            AdvertisementImageAssetPolicy.IsOwnedByAdvertisement(
                secondAdvertisement, assetId));
    }

    [Theory]
    [InlineData("https://example.com/image.png")]
    [InlineData("../other/image")]
    [InlineData("wayhunter/advertisements/../../secret")]
    [InlineData("wayhunter/advertisements")]
    [InlineData("")]
    public void UnsafeAssetIds_AreRejected(string assetId)
    {
        Assert.False(
            AdvertisementImageAssetPolicy.IsOwnedByAdvertisement(
                Guid.NewGuid(), assetId));
    }

    [Fact]
    public void EmptyAdvertisementId_IsRejected()
    {
        Assert.Throws<ArgumentException>(() =>
            AdvertisementImageAssetPolicy.CreateAssetId(
                Guid.Empty));

        Assert.False(
            AdvertisementImageAssetPolicy.IsOwnedByAdvertisement(
                Guid.Empty, "wayhunter/advertisements/example"));
    }

    [Fact]
    public void MissingOrMalformedSuffix_IsRejected()
    {
        var advertisementId = Guid.NewGuid();

        var prefix =
            $"wayhunter/advertisements/{advertisementId:D}/";

        Assert.False(
            AdvertisementImageAssetPolicy.IsOwnedByAdvertisement(
                advertisementId, prefix));

        Assert.False(
            AdvertisementImageAssetPolicy.IsOwnedByAdvertisement(
                advertisementId, prefix + "../foreign"));

        Assert.False(
            AdvertisementImageAssetPolicy.IsOwnedByAdvertisement(
                advertisementId, prefix + "not-a-guid"));
    }
}