namespace Wayhunter.Application.Modules.HomeAdsAnalytics.Interfaces;

/// <summary>
/// Uploads using a server-reserved immutable asset ID.
/// Caller must ensure the operation was durably persisted first.
/// </summary>
public interface IAdvertisementImageReservedStorage
{
    Task<AdvertisementStoredImage> UploadReservedAsync(
        Guid advertisementId,
        string assetId,
        AdvertisementValidatedImage image,
        CancellationToken cancellationToken = default);
}