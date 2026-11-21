using Wayhunter.Application.Modules.HomeAdsAnalytics.Interfaces;

namespace Wayhunter.Application.Modules.HomeAdsAnalytics.Services;

/// <summary>
/// Pure policy only. Does not persist records, contact Cloudinary,
/// issue SQL queries, or authorize actual asset deletion.
/// </summary>
public static class AdvertisementImageRecoveryPolicy
{
    public static bool CanTransition(
        AdvertisementImageRecoveryState current,
        AdvertisementImageRecoveryState next)
    {
        if (current == next)
            return false;

        return current switch
        {
            AdvertisementImageRecoveryState.PendingUpload =>
                next is AdvertisementImageRecoveryState.Uploaded
                    or AdvertisementImageRecoveryState.UploadOutcomeUnknown
                    or AdvertisementImageRecoveryState.ManualReview,

            AdvertisementImageRecoveryState.UploadOutcomeUnknown =>
                next is AdvertisementImageRecoveryState.Uploaded
                    or AdvertisementImageRecoveryState.ManualReview,

            AdvertisementImageRecoveryState.Uploaded =>
                next is AdvertisementImageRecoveryState.SqlOutcomeUnknown
                    or AdvertisementImageRecoveryState.Committed
                    or AdvertisementImageRecoveryState.Conflict
                    or AdvertisementImageRecoveryState.ManualReview,

            AdvertisementImageRecoveryState.SqlOutcomeUnknown =>
                next is AdvertisementImageRecoveryState.Committed
                    or AdvertisementImageRecoveryState.Conflict
                    or AdvertisementImageRecoveryState.ManualReview,

            AdvertisementImageRecoveryState.Conflict =>
                next is AdvertisementImageRecoveryState.CleanupPending
                    or AdvertisementImageRecoveryState.ManualReview,

            AdvertisementImageRecoveryState.CleanupPending =>
                next is AdvertisementImageRecoveryState.CleanupConfirmed
                    or AdvertisementImageRecoveryState.ManualReview,

            // Terminal states require an explicitly approved
            // administrative recovery workflow.
            _ => false
        };
    }

    public static AdvertisementImageUploadOperation PlanVerificationRetry(
        AdvertisementImageUploadOperation operation,
        Guid expectedClaimToken,
        DateTimeOffset nowUtc,
        int maximumRetries,
        TimeSpan retryDelay)
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (maximumRetries <= 0)
            throw new ArgumentOutOfRangeException(nameof(maximumRetries));

        if (retryDelay <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(retryDelay));

        if (expectedClaimToken == Guid.Empty ||
            operation.ClaimToken != expectedClaimToken ||
            operation.LeaseExpiresAtUtc is null ||
            operation.LeaseExpiresAtUtc <= nowUtc)
        {
            throw new InvalidOperationException(
                "A valid recovery claim is required.");
        }

        if (operation.State is not (
            AdvertisementImageRecoveryState.UploadOutcomeUnknown
            or AdvertisementImageRecoveryState.SqlOutcomeUnknown))
        {
            throw new InvalidOperationException(
                "Only uncertain operations may schedule verification retries.");
        }

        var nextCount = checked(operation.RetryCount + 1);
        var exhausted = nextCount >= maximumRetries;

        return operation with
        {
            RetryCount = nextCount,
            State = exhausted
                ? AdvertisementImageRecoveryState.ManualReview
                : operation.State,
            NextRetryAtUtc = exhausted
                ? null
                : nowUtc.Add(retryDelay),
            UpdatedAtUtc = nowUtc,
            LastErrorCode = exhausted
                ? "M7_RECOVERY_RETRY_EXHAUSTED"
                : "M7_RECOVERY_VERIFICATION_PENDING"
        };
    }
}