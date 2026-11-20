using Wayhunter.Application.Modules.HomeAdsAnalytics.Interfaces;

namespace Wayhunter.Application.Modules.HomeAdsAnalytics.Services;

/// <summary>
/// Pure precondition checks for recovery writes.
/// These checks DO NOT replace atomic database fencing.
/// </summary>
public static class AdvertisementImageRecoveryFencingPolicy
{
    public static bool HasValidClaim(
        AdvertisementImageUploadOperation current,
        Guid expectedClaimToken,
        DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(current);

        return expectedClaimToken != Guid.Empty &&
            current.ClaimToken == expectedClaimToken &&
            current.LeaseExpiresAtUtc.HasValue &&
            current.LeaseExpiresAtUtc.Value > nowUtc;
    }

    public static bool CanSave(
        AdvertisementImageUploadOperation current,
        AdvertisementImageUploadOperation proposed,
        long expectedRevision,
        Guid expectedClaimToken,
        DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(proposed);

        if (!HasValidClaim(current, expectedClaimToken, nowUtc))
            return false;

        if (expectedRevision < 0 ||
            expectedRevision == long.MaxValue ||
            current.Revision != expectedRevision ||
            proposed.Revision != expectedRevision + 1)
            return false;

        if (current.UploadOperationId == Guid.Empty ||
            current.AdvertisementId == Guid.Empty ||
            proposed.UploadOperationId != current.UploadOperationId ||
            proposed.AdvertisementId != current.AdvertisementId ||
            proposed.AdministratorId != current.AdministratorId ||
            proposed.PreviousImageUrl != current.PreviousImageUrl)
            return false;

        // A stale worker cannot change the active claim or lease.
        if (proposed.ClaimToken != current.ClaimToken ||
            proposed.LeaseExpiresAtUtc != current.LeaseExpiresAtUtc)
            return false;

        // Immutable identity of an already established uploaded asset.
        if (current.ProviderAssetId is not null &&
            proposed.ProviderAssetId != current.ProviderAssetId)
            return false;

        if (current.ReplacementImageUrl is not null &&
            proposed.ReplacementImageUrl != current.ReplacementImageUrl)
            return false;

        if (proposed.RetryCount < current.RetryCount ||
            proposed.UpdatedAtUtc < current.UpdatedAtUtc)
            return false;

        // Cleanup decisions require a separate authoritative SQL
        // reference verification and atomic deletion authorization.
        // This generic save guard must never grant cleanup.
        if (proposed.State is
            AdvertisementImageRecoveryState.CleanupPending or
            AdvertisementImageRecoveryState.CleanupConfirmed)
            return false;

        return proposed.State == current.State ||
            AdvertisementImageRecoveryPolicy.CanTransition(
                current.State,
                proposed.State);
    }
}