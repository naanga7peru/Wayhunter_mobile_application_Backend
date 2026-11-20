namespace Wayhunter.Application.Modules.HomeAdsAnalytics.Interfaces;

/// <summary>
/// Validated and metadata-stripped image ready for storage.
/// Callers must not pass unvalidated client file content.
/// </summary>
public sealed record AdvertisementValidatedImage(
    byte[] Content,
    string ContentType,
    string Extension);

/// <summary>
/// Storage result produced by a trusted server-side provider.
/// </summary>
public sealed record AdvertisementStoredImage(
    string AssetId,
    string HttpsUrl);

/// <summary>
/// M7-owned Advertisement image storage contract.
/// Implementations must generate their own asset identifiers,
/// use an M7-owned namespace and never overwrite existing assets.
/// </summary>
public interface IAdvertisementImageStorage
{
    Task<AdvertisementStoredImage> UploadAsync(
        Guid advertisementId,
        AdvertisementValidatedImage image,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cleanup is permitted only for verified M7-owned asset IDs.
    /// Never delete arbitrary URLs or client-supplied paths.
    /// </summary>
    Task<bool> TryDeleteOwnedAssetAsync(
        string assetId,
        CancellationToken cancellationToken = default);
}
