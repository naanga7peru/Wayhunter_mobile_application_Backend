namespace Wayhunter.Application.Modules.TripSafety.Memories;

public enum TripMemoryVisibility { Private = 0, Public = 1 }
public enum TripMemoryMediaState { Pending = 0, Attached = 1, CleanupClaimed = 2, Cleaned = 3 }

public sealed record TripMemory(Guid MemoryId, Guid TripPlanId, Guid? TripStopId, string OwnerUserId,
    Guid CreateOperationId, string Caption, TripMemoryVisibility Visibility,
    DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc, DateTimeOffset? DeletedAtUtc = null);

public sealed record TripMemoryMedia(Guid OperationId, Guid MemoryId, string OwnerUserId, string ContentType,
    long ContentLength, DateTimeOffset CreatedAtUtc, TripMemoryMediaState State);

public sealed record TripMemoryView(Guid MemoryId, Guid TripPlanId, Guid? TripStopId, string Caption,
    TripMemoryVisibility Visibility, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc,
    TripMemoryMediaMetadata? Media);

public sealed record TripMemoryMediaMetadata(string ContentType, long ContentLength);
public sealed record TripMemoryCreateResult(TripMemoryView Memory, bool Created);
public sealed record TripMemoryStoreCreateResult(TripMemory Memory, bool Created);

// This contract is backed by one transactional store when SQL is approved.
// The store must recheck the authoritative trip owner and optional stop binding on create.
public interface ITripMemoryStore
{
    bool IsAvailable { get; }

    // Unique (TripPlanId, OwnerUserId, CreateOperationId), with payload comparison and
    // tombstone retention even after soft deletion. Return only after commit.
    Task<TripMemoryStoreCreateResult> CreateAsync(TripMemory candidate, CancellationToken ct);
    Task<TripMemory?> FindAsync(Guid tripId, Guid memoryId, CancellationToken ct);
    Task<IReadOnlyList<TripMemory>> ListOwnedAsync(Guid tripId, string ownerId, CancellationToken ct);
    Task<TripMemoryMediaMetadata?> ReadAttachedMediaAsync(Guid memoryId, string ownerId, CancellationToken ct);

    // Atomically check ownership, hide the post and make its media cleanup-eligible.
    Task<bool> SoftDeleteAsync(Guid tripId, Guid memoryId, string ownerId, DateTimeOffset now, CancellationToken ct);

    // Reserve exactly one operation per live owned memory; identical retry returns it,
    // changed requests conflict. A cleaned/deleted operation must never be reused.
    Task<TripMemoryMedia> ReserveMediaAsync(TripMemory memory, TripMemoryMedia candidate, CancellationToken ct);
    Task<TripMemoryMedia?> FindMediaAsync(Guid memoryId, Guid operationId, string ownerId, CancellationToken ct);

    // Atomically attach only the matching live operation, or accept an exact attached retry.
    // The asset key/version must be globally exclusive and cannot be claimed for cleanup.
    Task AttachAsync(TripMemory memory, TripMemoryMedia operation, VerifiedMemoryAsset asset, CancellationToken ct);

    // CAS claim a deleted-memory or expired sealed reservation only after proving no live
    // reference can use the object. Persist the claim as a barrier against future attachment.
    // Retried claims retain the same frozen identity; unresolved CAS returns null.
    Task<MemoryCleanupClaim?> ReadCleanupClaimAsync(Guid operationId, CancellationToken ct);
    Task<TripMemoryMedia?> FindCleanupCandidateAsync(Guid operationId, CancellationToken ct);
    Task<MemoryCleanupClaim?> ClaimCleanupAsync(TripMemoryMedia candidate, VerifiedMemoryAsset asset, CancellationToken ct);
    Task CompleteCleanupAsync(MemoryCleanupClaim claim, CancellationToken ct);
}

public sealed record MemoryUploadGrant(Guid OperationId, Uri UploadUrl, DateTimeOffset ExpiresAtUtc);
public sealed record VerifiedMemoryAsset(Guid OperationId, Guid MemoryId, string OwnerUserId, string ObjectKey,
    string ObjectVersion, string ContentType, long ContentLength, bool IsPrivate, bool ContentDecoded);
public sealed record MemoryCleanupClaim(Guid ClaimId, TripMemoryMedia Operation, VerifiedMemoryAsset Asset);

public interface ITripMemoryMediaProvider
{
    // Grants are short-lived, private, scoped to one operation/object and constrained by type
    // and size, expiring no later than 15 minutes after reservation creation. Reissuing an
    // operation cannot allocate another object, extend expiry, or overwrite a sealed object.
    Task<MemoryUploadGrant> IssueAsync(TripMemoryMedia operation, CancellationToken ct);

    // Resolve and seal the operation's immutable asset using trusted provider evidence.
    // Decode the actual content to reject corrupt media; never trust client asset IDs.
    Task<VerifiedMemoryAsset?> InspectAsync(TripMemoryMedia operation, CancellationToken ct);
    Task<Stream> OpenVerifiedContentAsync(VerifiedMemoryAsset asset, CancellationToken ct);

    // Condition deletion on this exact immutable private object version and ownership.
    // Already absent is success only for the same proven identity; conflicts fail closed.
    Task DeleteVerifiedVersionAsync(VerifiedMemoryAsset asset, CancellationToken ct);
}

public sealed class TripMemoryUnavailableException : Exception;
public sealed class TripMemoryConflictException(string message) : Exception(message);
