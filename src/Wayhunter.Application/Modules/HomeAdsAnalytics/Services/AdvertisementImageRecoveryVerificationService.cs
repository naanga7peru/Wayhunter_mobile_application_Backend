using Wayhunter.Application.Modules.HomeAdsAnalytics.Interfaces;

namespace Wayhunter.Application.Modules.HomeAdsAnalytics.Services;

/// <summary>
/// Offline verification orchestration. No Cloudinary writes/deletes.
/// Requires a durable, atomically fenced repository before runtime use.
/// </summary>
public sealed class AdvertisementImageRecoveryVerificationService
{
    private readonly IAdvertisementImageRecoveryRepository _repository;
    private readonly IAdvertisementImageReferenceVerifier _verifier;

    public AdvertisementImageRecoveryVerificationService(
        IAdvertisementImageRecoveryRepository repository,
        IAdvertisementImageReferenceVerifier verifier)
    {
        _repository = repository ??
            throw new ArgumentNullException(nameof(repository));

        _verifier = verifier ??
            throw new ArgumentNullException(nameof(verifier));
    }

    public async Task<bool> VerifyAndScheduleAsync(
        Guid operationId,
        Guid claimToken,
        DateTimeOffset nowUtc,
        TimeSpan leaseDuration,
        int maximumRetries,
        TimeSpan retryDelay,
        CancellationToken cancellationToken = default)
    {
        if (operationId == Guid.Empty || claimToken == Guid.Empty)
            throw new ArgumentException("Valid operation and claim IDs required.");

        if (leaseDuration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(leaseDuration));

        // The repository must acquire this claim atomically.
        var claimed = await _repository.TryClaimAsync(
            operationId,
            claimToken,
            nowUtc,
            nowUtc.Add(leaseDuration),
            cancellationToken);

        if (claimed is null)
            return false;

        if (!AdvertisementImageRecoveryFencingPolicy.HasValidClaim(
                claimed, claimToken, nowUtc))
        {
            return false;
        }

        if (claimed.State is not (
            AdvertisementImageRecoveryState.UploadOutcomeUnknown
            or AdvertisementImageRecoveryState.SqlOutcomeUnknown))
        {
            return false;
        }

        if (claimed.State ==
            AdvertisementImageRecoveryState.SqlOutcomeUnknown &&
            !string.IsNullOrWhiteSpace(claimed.ProviderAssetId) &&
            !string.IsNullOrWhiteSpace(claimed.ReplacementImageUrl))
        {
            // Verification is informational only at this stage.
            // A reference result alone cannot prove which operation
            // committed, nor authorize Cloudinary deletion.
            await _verifier.VerifyAsync(
                claimed.AdvertisementId,
                claimed.UploadOperationId,
                claimed.ProviderAssetId,
                claimed.ReplacementImageUrl,
                cancellationToken);
        }

        var planned = AdvertisementImageRecoveryPolicy
            .PlanVerificationRetry(
                claimed,
                claimToken,
                nowUtc,
                maximumRetries,
                retryDelay);

        planned = planned with
        {
            Revision = checked(claimed.Revision + 1)
        };

        if (!AdvertisementImageRecoveryFencingPolicy.CanSave(
                claimed,
                planned,
                claimed.Revision,
                claimToken,
                nowUtc))
        {
            return false;
        }

        // SQL implementation must additionally enforce revision,
        // claim token and lease expiry atomically.
        return await _repository.TrySaveAsync(
            planned,
            claimed.Revision,
            claimToken,
            cancellationToken);
    }
}