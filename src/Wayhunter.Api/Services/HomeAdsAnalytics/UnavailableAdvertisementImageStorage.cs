using Wayhunter.Application.Modules.HomeAdsAnalytics.Interfaces;

namespace Wayhunter.Api.Services.HomeAdsAnalytics;

/// <summary>
/// Fail-closed storage implementation used until
/// Cloudinary provider configuration and activation
/// receive separate approval.
/// </summary>
public sealed class UnavailableAdvertisementImageStorage
    : IAdvertisementImageStorage
{
    public Task<AdvertisementStoredImage> UploadAsync(
        Guid advertisementId,
        AdvertisementValidatedImage image,
        CancellationToken cancellationToken = default)
    {
            if (advertisementId == Guid.Empty)
                throw new ArgumentException("Invalid advertisement ID.", nameof(advertisementId));
        cancellationToken.ThrowIfCancellationRequested();

        throw new InvalidOperationException(
            "Advertisement image storage is not enabled.");
    }

    public Task<bool> TryDeleteOwnedAssetAsync(
        string assetId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(false);
    }
}
