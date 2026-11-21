using Wayhunter.Application.Modules.HomeAdsAnalytics.Interfaces;
using Wayhunter.Application.Modules.HomeAdsAnalytics.Validators;

namespace Wayhunter.Application.Modules.HomeAdsAnalytics.Services;

/// <summary>
/// Prepares a durable operation before external provider activity.
/// No upload or cleanup is performed here.
/// </summary>
public sealed class AdvertisementImageUploadPreparationService
{
    private readonly IAdvertisementImageRecoveryRepository _repository;

    public AdvertisementImageUploadPreparationService(
        IAdvertisementImageRecoveryRepository repository)
    {
        _repository = repository ??
            throw new ArgumentNullException(nameof(repository));
    }

    public async Task<AdvertisementImageUploadOperation> PrepareAsync(
        Guid uploadOperationId,
        Guid advertisementId,
        string trustedAdministratorId,
        string previousImageUrl,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        if (uploadOperationId == Guid.Empty)
            throw new ArgumentException("Invalid operation ID.");

        if (advertisementId == Guid.Empty)
            throw new ArgumentException("Invalid advertisement ID.");

        if (string.IsNullOrWhiteSpace(trustedAdministratorId))
            throw new ArgumentException(
                "Trusted Administrator identity is required.");

        if (!AdvertisementUrlValidator.IsValidImageUrl(previousImageUrl))
            throw new ArgumentException(
                "Valid previous HTTPS image URL is required.");

        cancellationToken.ThrowIfCancellationRequested();

        // Stable identifier derived from the operation ID.
        // It must be reused for provider-state recovery.
        var providerAssetId =
            $"wayhunter/advertisements/{advertisementId:D}/" +
            $"{uploadOperationId:N}";

        var operation = new AdvertisementImageUploadOperation
        {
            UploadOperationId = uploadOperationId,
            AdvertisementId = advertisementId,
            AdministratorId = trustedAdministratorId,
            PreviousImageUrl = previousImageUrl,
            ProviderAssetId = providerAssetId,
            State = AdvertisementImageRecoveryState.PendingUpload,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
            RetryCount = 0,
            Revision = 0
        };

        // The implementation must commit the record durably.
        // Returning true before the SQL commit is forbidden.
        var created = await _repository.TryCreateAsync(
            operation,
            cancellationToken);

        if (!created)
        {
            // No provider upload has been attempted.
            // Caller must inspect an existing operation separately
            // rather than create another provider asset.
            throw new InvalidOperationException(
                "Durable upload operation creation was not confirmed.");
        }

        return operation;
    }
}