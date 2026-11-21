using Wayhunter.Application.Modules.HomeAdsAnalytics.Dtos;
using Wayhunter.Application.Modules.HomeAdsAnalytics.Interfaces;
using Wayhunter.Application.Modules.HomeAdsAnalytics.Validators;

namespace Wayhunter.Api.Services.HomeAdsAnalytics;

public sealed class AdvertisementImageLifecycleService
{
    private readonly IAdvertisementImageStorage _storage;
    private readonly IAdvertisementImageUpdateRepository _repository;

    public AdvertisementImageLifecycleService(
        IAdvertisementImageStorage storage,
        IAdvertisementImageUpdateRepository repository)
    {
        _storage = storage ??
            throw new ArgumentNullException(nameof(storage));

        _repository = repository ??
            throw new ArgumentNullException(nameof(repository));
    }

    public async Task<AdvertisementDto?> ReplaceAsync(
        Guid advertisementId,
        string expectedImageUrl,
        AdvertisementValidatedImage image,
        CancellationToken cancellationToken = default)
    {
        if (advertisementId == Guid.Empty)
            throw new ArgumentException("Invalid advertisement ID.");

        if (!AdvertisementUrlValidator.IsValidImageUrl(expectedImageUrl))
            throw new ArgumentException(
                "Current image URL must be a valid HTTPS image URL.",
                nameof(expectedImageUrl));

        ArgumentNullException.ThrowIfNull(image);

        if (image.Content is null || image.Content.Length == 0)
            throw new ArgumentException("Validated image content is empty.");

        cancellationToken.ThrowIfCancellationRequested();

        var stored = await _storage.UploadAsync(advertisementId, image, cancellationToken);

        // Fail closed: validation failure does not authorize deletion.
        // Reconciliation requires future durable SQL operation records.
        if (string.IsNullOrWhiteSpace(stored.AssetId))
            throw new InvalidOperationException(
                "Storage returned an invalid asset identifier.");

        if (!AdvertisementUrlValidator.IsValidImageUrl(stored.HttpsUrl))
            throw new InvalidOperationException(
                "Storage returned an invalid HTTPS URL.");

        // Important:
        // An exception from ExecuteUpdateAsync does not prove that
        // the SQL update was rolled back.
        // Do not delete the new asset on repository exceptions.
        // Such assets require later reconciliation.
        var updated = await _repository.UpdateImageUrlAsync(
            advertisementId,
            expectedImageUrl,
            stored.HttpsUrl,
            cancellationToken);

        // A CAS conflict never authorizes Cloudinary deletion.
        // Keep the uploaded asset pending future SQL reconciliation.
        if (updated is null)
        {
            return null;
        }

        return updated;
    }
}
