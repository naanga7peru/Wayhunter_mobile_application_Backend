namespace Wayhunter.Application.Modules.HomeAdsAnalytics.Interfaces;

// Provisional application contract.
// DB Coordinator retains authority over the final SQL schema.

public enum AdvertisementImageRecoveryState
{
    PendingUpload = 0,
    UploadOutcomeUnknown = 1,
    Uploaded = 2,
    SqlOutcomeUnknown = 3,
    Committed = 4,
    Conflict = 5,
    CleanupPending = 6,
    CleanupConfirmed = 7,
    ManualReview = 8
}

public sealed record AdvertisementImageUploadOperation
{
    public required Guid UploadOperationId { get; init; }
    public required Guid AdvertisementId { get; init; }

    // Trusted authenticated server-side identity.
    public required string AdministratorId { get; init; }

    public required string PreviousImageUrl { get; init; }

    public string? ReplacementImageUrl { get; init; }
    public string? ProviderAssetId { get; init; }

    public required AdvertisementImageRecoveryState State { get; init; }

    public required DateTimeOffset CreatedAtUtc { get; init; }
    public required DateTimeOffset UpdatedAtUtc { get; init; }

    public DateTimeOffset? CompletedAtUtc { get; init; }
    public DateTimeOffset? NextRetryAtUtc { get; init; }

    public int RetryCount { get; init; }
    public string? LastErrorCode { get; init; }

    // Fencing: stale recovery workers must not update an operation.
    public Guid? ClaimToken { get; init; }
    public DateTimeOffset? LeaseExpiresAtUtc { get; init; }

    // Application-level expected revision.
    // Final SQL rowversion mapping requires DB approval.
    public long Revision { get; init; }

    public string? CleanupDecisionCode { get; init; }
    public DateTimeOffset? CleanupDecisionAtUtc { get; init; }
}

public interface IAdvertisementImageRecoveryRepository
{
    // Must be durably persisted before any provider upload.
    // Same operation ID must not create multiple operations.
    Task<bool> TryCreateAsync(
        AdvertisementImageUploadOperation operation,
        CancellationToken cancellationToken = default);

    Task<AdvertisementImageUploadOperation?> GetAsync(
        Guid uploadOperationId,
        CancellationToken cancellationToken = default);

    // Atomic claim with lease and revision fencing.
    Task<AdvertisementImageUploadOperation?> TryClaimAsync(
        Guid uploadOperationId,
        Guid claimToken,
        DateTimeOffset nowUtc,
        DateTimeOffset leaseExpiresAtUtc,
        CancellationToken cancellationToken = default);

    // Atomic conditional update; stale workers must fail.
    Task<bool> TrySaveAsync(
        AdvertisementImageUploadOperation operation,
        long expectedRevision,
        Guid expectedClaimToken,
        CancellationToken cancellationToken = default);
}

public enum AdvertisementAssetReferenceDecision
{
    Unknown = 0,
    Referenced = 1,
    UnreferencedButUnsafe = 2,
    SafeForCleanup = 3
}

public sealed record AdvertisementAssetReferenceResult(
    AdvertisementAssetReferenceDecision Decision,
    string ReasonCode);

// A deletion decision requires an authoritative, concurrency-safe
// SQL reference check. An ordinary unlocked SELECT is insufficient.
public interface IAdvertisementImageReferenceVerifier
{
    Task<AdvertisementAssetReferenceResult> VerifyAsync(
        Guid advertisementId,
        Guid uploadOperationId,
        string providerAssetId,
        string replacementImageUrl,
        CancellationToken cancellationToken = default);
}