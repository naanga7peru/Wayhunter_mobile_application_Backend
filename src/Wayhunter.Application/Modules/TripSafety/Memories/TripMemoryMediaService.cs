using System.Buffers.Binary;

namespace Wayhunter.Application.Modules.TripSafety.Memories;

public sealed class TripMemoryMediaService(
    TripMemoryService memories, ITripMemoryStore store, ITripMemoryMediaProvider provider, TimeProvider clock)
{
    // Conservative implementation ceilings for provider-independent verification.
    // Production types and limits require approval.
    private const int PhotoLimit = 5 * 1024 * 1024;
    private const int VideoLimit = 16 * 1024 * 1024;

    public async Task<MemoryUploadGrant> BeginAsync(Guid tripId, Guid memoryId, string userId, Guid operationId,
        string? contentType, long contentLength, CancellationToken ct)
    {
        TripMemoryService.ValidateId(operationId);
        ValidateMedia(contentType, contentLength);
        var memory = await memories.FindOwnedAsync(tripId, memoryId, userId, ct);
        var operation = await store.ReserveMediaAsync(memory, new TripMemoryMedia(operationId, memoryId,
            userId, contentType!, contentLength, clock.GetUtcNow(), TripMemoryMediaState.Pending), ct);
        if (operation.OperationId != operationId || operation.MemoryId != memoryId || operation.OwnerUserId != userId ||
            operation.ContentType != contentType || operation.ContentLength != contentLength ||
            operation.State != TripMemoryMediaState.Pending)
            throw new TripMemoryConflictException("The upload operation cannot issue a grant.");

        var expiresBy = operation.CreatedAtUtc.AddMinutes(15);
        if (expiresBy <= clock.GetUtcNow())
            throw new TripMemoryConflictException("The upload grant window has elapsed.");

        var grant = await provider.IssueAsync(operation, ct);
        var now = clock.GetUtcNow();
        if (grant.OperationId != operationId || !grant.UploadUrl.IsAbsoluteUri ||
            grant.UploadUrl.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(grant.UploadUrl.UserInfo) ||
            !string.IsNullOrEmpty(grant.UploadUrl.Fragment) ||
            grant.ExpiresAtUtc <= now || grant.ExpiresAtUtc > expiresBy)
            throw new TripMemoryUnavailableException();
        return grant;
    }

    public async Task<TripMemoryMediaMetadata> CompleteAsync(Guid tripId, Guid memoryId, string userId,
        Guid operationId, CancellationToken ct)
    {
        TripMemoryService.ValidateId(operationId);
        var memory = await memories.FindOwnedAsync(tripId, memoryId, userId, ct);
        var operation = await store.FindMediaAsync(memoryId, operationId, userId, ct);
        if (operation is null || operation.MemoryId != memoryId || operation.OwnerUserId != userId ||
            operation.OperationId != operationId)
            throw new KeyNotFoundException("Upload operation was not found.");
        if (operation.State == TripMemoryMediaState.Attached)
            return await store.ReadAttachedMediaAsync(memoryId, userId, ct) ?? throw new TripMemoryUnavailableException();
        if (operation.State != TripMemoryMediaState.Pending)
            throw new TripMemoryConflictException("The upload operation cannot be completed.");

        var asset = await provider.InspectAsync(operation, ct);
        if (asset is null || !Matches(operation, asset) || !asset.ContentDecoded)
            throw new TripMemoryConflictException("Private uploaded media could not be verified.");
        ValidateMedia(asset.ContentType, asset.ContentLength);

        await using var content = await provider.OpenVerifiedContentAsync(asset, ct);
        var bytes = new byte[(int)asset.ContentLength];
        try
        {
            await content.ReadExactlyAsync(bytes, ct);
        }
        catch (EndOfStreamException)
        {
            throw new ArgumentException("Media content is truncated.");
        }

        var extra = new byte[1];
        if (await content.ReadAsync(extra, ct) != 0 || !HasExpectedSignature(asset.ContentType, bytes))
            throw new ArgumentException("Media content does not match its declared size and type.");

        await store.AttachAsync(memory, operation, asset, ct);
        return new(asset.ContentType, asset.ContentLength);
    }

    internal static bool Matches(TripMemoryMedia operation, VerifiedMemoryAsset asset) =>
        asset.IsPrivate && asset.OperationId == operation.OperationId && asset.MemoryId == operation.MemoryId &&
        string.Equals(asset.OwnerUserId, operation.OwnerUserId, StringComparison.Ordinal) &&
        asset.ContentType == operation.ContentType && asset.ContentLength == operation.ContentLength &&
        !string.IsNullOrWhiteSpace(asset.ObjectKey) && asset.ObjectKey.Length <= 450 &&
        !string.IsNullOrWhiteSpace(asset.ObjectVersion) && asset.ObjectVersion.Length <= 256;

    private static void ValidateMedia(string? type, long length)
    {
        var maximum = type switch
        {
            "image/jpeg" or "image/png" => PhotoLimit,
            "video/mp4" => VideoLimit,
            _ => throw new ArgumentException("Unsupported media content type.")
        };
        if (length <= 0 || length > maximum)
            throw new ArgumentException("Invalid media size.");
    }

    private static bool HasExpectedSignature(string type, ReadOnlySpan<byte> bytes) => type switch
    {
        "image/jpeg" => bytes.Length >= 5 && bytes.StartsWith(new byte[] { 255, 216, 255 }) &&
            bytes.EndsWith(new byte[] { 255, 217 }),
        "image/png" => bytes.Length >= 45 && bytes.StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) &&
            bytes.Slice(8, 8).SequenceEqual(new byte[] { 0, 0, 0, 13, 73, 72, 68, 82 }) &&
            bytes[^12..].SequenceEqual(new byte[] { 0, 0, 0, 0, 73, 69, 78, 68, 174, 66, 96, 130 }),
        "video/mp4" => bytes.Length >= 24 && bytes.Slice(4, 4).SequenceEqual("ftyp"u8) &&
            BinaryPrimitives.ReadUInt32BigEndian(bytes) is >= 16 and var boxSize && boxSize <= bytes.Length - 8,
        _ => false
    };
}

public sealed class TripMemoryCleanupService(ITripMemoryStore store, ITripMemoryMediaProvider provider)
{
    public async Task<bool> CleanAsync(Guid operationId, CancellationToken ct)
    {
        TripMemoryService.ValidateId(operationId);
        var claim = await store.ReadCleanupClaimAsync(operationId, ct);
        if (claim is null)
        {
            var candidate = await store.FindCleanupCandidateAsync(operationId, ct);
            if (candidate is null || candidate.OperationId != operationId ||
                candidate.State is not (TripMemoryMediaState.Pending or TripMemoryMediaState.Attached))
                return false;

            var asset = await provider.InspectAsync(candidate, ct);
            if (asset is null || !TripMemoryMediaService.Matches(candidate, asset))
                return false;

            claim = await store.ClaimCleanupAsync(candidate, asset, ct);
            if (claim is not null &&
                (claim.Asset != asset || claim.Operation != candidate with { State = TripMemoryMediaState.CleanupClaimed }))
                return false;
        }

        if (claim is null || claim.ClaimId == Guid.Empty || claim.Operation.OperationId != operationId ||
            claim.Operation.State != TripMemoryMediaState.CleanupClaimed ||
            !TripMemoryMediaService.Matches(claim.Operation, claim.Asset))
            return false;

        await provider.DeleteVerifiedVersionAsync(claim.Asset, ct);
        await store.CompleteCleanupAsync(claim, ct);
        return true;
    }
}
