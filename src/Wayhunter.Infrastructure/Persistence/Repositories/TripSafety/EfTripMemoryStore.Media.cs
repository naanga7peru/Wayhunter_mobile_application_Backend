using Microsoft.EntityFrameworkCore;
using Wayhunter.Application.Modules.TripSafety.Memories;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Wayhunter.Infrastructure.Persistence.Models.TripSafety;

namespace Wayhunter.Infrastructure.Persistence.Repositories.TripSafety;

public sealed partial class EfTripMemoryStore
{
    public Task<TripMemoryMedia> ReserveMediaAsync(TripMemory memory, TripMemoryMedia candidate, CancellationToken ct) =>
        WriteAsync(async (db, token) =>
        {
            await RequireLiveMemoryAsync(db, memory, token);
            if (candidate.MemoryId != memory.MemoryId || candidate.OwnerUserId != memory.OwnerUserId ||
                candidate.State != TripMemoryMediaState.Pending)
                throw new TripMemoryConflictException("Invalid upload reservation.");

            var reservations = await db.TripMemoryMedia.Where(row =>
                row.MemoryId == memory.MemoryId || row.OperationId == candidate.OperationId).Take(2).ToListAsync(token);
            if (reservations.Count > 1)
                throw new TripMemoryConflictException("The upload operation is already bound.");
            var existing = reservations.SingleOrDefault();
            if (existing is not null)
            {
                var stored = ToMedia(existing, memory.OwnerUserId);
                if (stored != candidate with { CreatedAtUtc = stored.CreatedAtUtc })
                    throw new TripMemoryConflictException("The upload operation is already bound.");
                return stored;
            }

            db.TripMemoryMedia.Add(new TripMemoryMediaRow
            {
                MemoryId = memory.MemoryId, OperationId = candidate.OperationId,
                ContentType = candidate.ContentType, ContentLength = candidate.ContentLength,
                State = candidate.State, CreatedAtUtc = candidate.CreatedAtUtc
            });
            await db.SaveChangesAsync(token);
            return candidate;
        }, ct);

    public async Task<TripMemoryMedia?> FindMediaAsync(Guid memoryId, Guid operationId, string ownerId, CancellationToken ct)
    {
        await using var db = await contexts.CreateDbContextAsync(ct);
        var pair = await ReadOperationAsync(db, operationId, ct);
        return pair is not null && pair.Memory.MemoryId == memoryId && pair.Memory.DeletedAtUtc is null &&
            string.Equals(pair.Memory.OwnerUserId, ownerId, StringComparison.Ordinal)
            ? ToMedia(pair.Media, ownerId) : null;
    }

    public Task AttachAsync(TripMemory memory, TripMemoryMedia operation, VerifiedMemoryAsset asset, CancellationToken ct) =>
        WriteAsync(async (db, token) =>
        {
            await RequireLiveMemoryAsync(db, memory, token);
            var row = await db.TripMemoryMedia.SingleOrDefaultAsync(item => item.OperationId == operation.OperationId, token);
            if (row is null || row.MemoryId != memory.MemoryId ||
                ToMedia(row, memory.OwnerUserId) with { State = operation.State } != operation ||
                !TripMemoryMediaService.Matches(operation, asset) || !asset.ContentDecoded)
                throw new TripMemoryConflictException("The upload operation no longer matches.");

            if (row.State == TripMemoryMediaState.Attached && SameVersion(row, asset))
                return true;
            if (row.State != TripMemoryMediaState.Pending || await HasOtherReferenceAsync(db, row, asset, token))
                throw new TripMemoryConflictException("The upload cannot be attached.");

            row.ObjectKey = asset.ObjectKey;
            row.ObjectVersion = asset.ObjectVersion;
            row.State = TripMemoryMediaState.Attached;
            await db.SaveChangesAsync(token);
            return true;
        }, ct);

    public async Task<MemoryCleanupClaim?> ReadCleanupClaimAsync(Guid operationId, CancellationToken ct)
    {
        await using var db = await contexts.CreateDbContextAsync(ct);
        var pair = await ReadOperationAsync(db, operationId, ct);
        if (pair?.Media is not { State: TripMemoryMediaState.CleanupClaimed, CleanupClaimId: { } id,
                ObjectKey: { } key, ObjectVersion: { } version })
            return null;
        var operation = ToMedia(pair.Media, pair.Memory.OwnerUserId);
        return new(id, operation, new(operation.OperationId, operation.MemoryId, operation.OwnerUserId,
            key, version, operation.ContentType, operation.ContentLength, true, false));
    }

    public async Task<TripMemoryMedia?> FindCleanupCandidateAsync(Guid operationId, CancellationToken ct)
    {
        await using var db = await contexts.CreateDbContextAsync(ct);
        var pair = await ReadOperationAsync(db, operationId, ct);
        return pair is not null && IsCleanupEligible(pair) ? ToMedia(pair.Media, pair.Memory.OwnerUserId) : null;
    }

    public Task<MemoryCleanupClaim?> ClaimCleanupAsync(TripMemoryMedia candidate, VerifiedMemoryAsset asset, CancellationToken ct) =>
        WriteAsync<MemoryCleanupClaim?>(async (db, token) =>
        {
            var pair = await ReadOperationAsync(db, candidate.OperationId, token);
            if (pair is null || ToMedia(pair.Media, pair.Memory.OwnerUserId) != candidate ||
                !IsCleanupEligible(pair) || !TripMemoryMediaService.Matches(candidate, asset) ||
                (pair.Media.State == TripMemoryMediaState.Attached && !SameVersion(pair.Media, asset)) ||
                await HasOtherReferenceAsync(db, pair.Media, asset, token))
                return null;

            var row = pair.Media;
            var claimId = Guid.NewGuid();
            row.CleanupClaimId = claimId;
            row.ObjectKey = asset.ObjectKey;
            row.ObjectVersion = asset.ObjectVersion;
            row.State = TripMemoryMediaState.CleanupClaimed;
            await db.SaveChangesAsync(token);
            return new(claimId, ToMedia(row, pair.Memory.OwnerUserId), asset);
        }, ct);

    public Task CompleteCleanupAsync(MemoryCleanupClaim claim, CancellationToken ct) =>
        WriteAsync(async (db, token) =>
        {
            var pair = await ReadOperationAsync(db, claim.Operation.OperationId, token);
            if (pair is null || pair.Media.CleanupClaimId != claim.ClaimId || claim.ClaimId == Guid.Empty ||
                !SameVersion(pair.Media, claim.Asset) || !TripMemoryMediaService.Matches(claim.Operation, claim.Asset) ||
                pair.Media.State is not (TripMemoryMediaState.CleanupClaimed or TripMemoryMediaState.Cleaned) ||
                ToMedia(pair.Media, pair.Memory.OwnerUserId) with { State = TripMemoryMediaState.CleanupClaimed } != claim.Operation)
                throw new TripMemoryConflictException("The cleanup claim no longer matches.");

            if (pair.Media.State == TripMemoryMediaState.CleanupClaimed)
            {
                pair.Media.State = TripMemoryMediaState.Cleaned;
                pair.Media.CleanedAtUtc = clock.GetUtcNow();
                await db.SaveChangesAsync(token);
            }
            return true;
        }, ct);

    private bool IsCleanupEligible(MediaAndMemory pair) => pair.Media.State switch
    {
        TripMemoryMediaState.Pending => pair.Media.CreatedAtUtc <= clock.GetUtcNow().AddMinutes(-15),
        TripMemoryMediaState.Attached => pair.Memory.DeletedAtUtc.HasValue,
        _ => false
    };

    private static async Task RequireLiveMemoryAsync(WayhunterSharedDbContext db, TripMemory memory, CancellationToken ct)
    {
        var scope = await (from row in db.TripMemories
                           join trip in db.Trips on row.TripPlanId equals trip.Id
                           where row.MemoryId == memory.MemoryId && row.TripPlanId == memory.TripPlanId && row.DeletedAtUtc == null
                           select new { row.OwnerUserId, TripOwner = trip.OwnerUserId }).SingleOrDefaultAsync(ct);
        if (scope is null || scope.OwnerUserId != memory.OwnerUserId || scope.TripOwner != memory.OwnerUserId)
            throw new KeyNotFoundException("Memory was not found.");
    }

    private static Task<MediaAndMemory?> ReadOperationAsync(WayhunterSharedDbContext db, Guid operation, CancellationToken ct) =>
        (from media in db.TripMemoryMedia
         join memory in db.TripMemories on media.MemoryId equals memory.MemoryId
         where media.OperationId == operation
         select new MediaAndMemory(media, memory)).SingleOrDefaultAsync(ct);

    private static Task<bool> HasOtherReferenceAsync(WayhunterSharedDbContext db, TripMemoryMediaRow row,
        VerifiedMemoryAsset asset, CancellationToken ct) =>
        db.TripMemoryMedia.AnyAsync(item => item.OperationId != row.OperationId && item.ObjectKey == asset.ObjectKey, ct);

    private static bool SameVersion(TripMemoryMediaRow row, VerifiedMemoryAsset asset) =>
        row.ObjectKey == asset.ObjectKey && row.ObjectVersion == asset.ObjectVersion;

    private static TripMemoryMedia ToMedia(TripMemoryMediaRow row, string owner) =>
        new(row.OperationId, row.MemoryId, owner, row.ContentType, row.ContentLength, row.CreatedAtUtc, row.State);

    private sealed record MediaAndMemory(TripMemoryMediaRow Media, TripMemoryRow Memory);
}
