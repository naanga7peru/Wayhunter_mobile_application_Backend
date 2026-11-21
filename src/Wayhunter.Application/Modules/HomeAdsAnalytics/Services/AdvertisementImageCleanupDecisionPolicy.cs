using Wayhunter.Application.Modules.HomeAdsAnalytics.Interfaces;

namespace Wayhunter.Application.Modules.HomeAdsAnalytics.Services;

public enum AdvertisementImageCleanupAssessment
{
    Preserve = 0,
    RequiresAtomicSqlReservation = 1
}

/// <summary>
/// Pure, fail-closed preliminary assessment.
/// NEVER authorizes direct Cloudinary deletion.
/// </summary>
public static class AdvertisementImageCleanupDecisionPolicy
{
    public static AdvertisementImageCleanupAssessment Assess(
        AdvertisementImageUploadOperation operation,
        AdvertisementAssetReferenceResult references,
        bool backendOwnershipVerified,
        Guid expectedClaimToken,
        DateTimeOffset nowUtc,
        TimeSpan gracePeriod)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(references);

        if (gracePeriod <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(gracePeriod));

        if (!backendOwnershipVerified ||
            operation.UploadOperationId == Guid.Empty ||
            operation.AdvertisementId == Guid.Empty ||
            string.IsNullOrWhiteSpace(operation.ProviderAssetId) ||
            string.IsNullOrWhiteSpace(operation.ReplacementImageUrl))
        {
            return AdvertisementImageCleanupAssessment.Preserve;
        }

        if (operation.State != AdvertisementImageRecoveryState.Conflict)
            return AdvertisementImageCleanupAssessment.Preserve;

        if (!AdvertisementImageRecoveryFencingPolicy.HasValidClaim(
                operation, expectedClaimToken, nowUtc))
        {
            return AdvertisementImageCleanupAssessment.Preserve;
        }

        // The grace period must expire after the latest operation update.
        // A later update restarts this conservative waiting period.
        if (operation.UpdatedAtUtc > nowUtc ||
            nowUtc - operation.UpdatedAtUtc < gracePeriod)
        {
            return AdvertisementImageCleanupAssessment.Preserve;
        }

        if (references.Decision !=
            AdvertisementAssetReferenceDecision.SafeForCleanup)
        {
            return AdvertisementImageCleanupAssessment.Preserve;
        }

        // Even this result is NOT permission to call provider deletion.
        // A DB-backed worker must first acquire an atomic, auditable
        // reservation that fences concurrent references.
        return AdvertisementImageCleanupAssessment
            .RequiresAtomicSqlReservation;
    }
}