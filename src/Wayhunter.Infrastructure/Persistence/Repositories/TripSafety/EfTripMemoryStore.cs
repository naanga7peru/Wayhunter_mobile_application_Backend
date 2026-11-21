using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Wayhunter.Application.Modules.TripSafety.Memories;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Wayhunter.Infrastructure.Persistence.Models.TripSafety;

namespace Wayhunter.Infrastructure.Persistence.Repositories.TripSafety;

// Deliberately unregistered until the schema and provider contracts are approved.
public sealed partial class EfTripMemoryStore(IDbContextFactory<WayhunterSharedDbContext> contexts, TimeProvider clock)
    : ITripMemoryStore
{
    private const string StopForeignKey = "FK_TripSafety_TripMemories_TripStops_TripStopId_TripPlanId";
    public bool IsAvailable => true;

    public Task<TripMemoryStoreCreateResult> CreateAsync(TripMemory candidate, CancellationToken ct) =>
        WriteAsync(async (db, token) =>
        {
            var owner = await db.Trips.Where(trip => trip.Id == candidate.TripPlanId)
                .Select(trip => trip.OwnerUserId).SingleOrDefaultAsync(token);
            if (!string.Equals(owner, candidate.OwnerUserId, StringComparison.Ordinal))
                throw new KeyNotFoundException("Trip was not found.");

            var existing = await db.TripMemories.SingleOrDefaultAsync(row => row.TripPlanId == candidate.TripPlanId &&
                row.OwnerUserId == candidate.OwnerUserId && row.CreateOperationId == candidate.CreateOperationId, token);
            if (existing is not null)
            {
                if (existing.DeletedAtUtc.HasValue || existing.TripStopId != candidate.TripStopId ||
                    existing.Caption != candidate.Caption || existing.Visibility != candidate.Visibility)
                    throw new TripMemoryConflictException("Create operation is already bound to a different or deleted memory.");

                return new TripMemoryStoreCreateResult(ToMemory(existing), false);
            }

            if (candidate.TripStopId is { } stopId &&
                !await db.TripStops.AnyAsync(stop => stop.Id == stopId && stop.TripPlanId == candidate.TripPlanId, token))
                throw new TripMemoryConflictException("Trip stop is no longer available.");

            db.TripMemories.Add(new TripMemoryRow
            {
                MemoryId = candidate.MemoryId, TripPlanId = candidate.TripPlanId, TripStopId = candidate.TripStopId,
                OwnerUserId = candidate.OwnerUserId, CreateOperationId = candidate.CreateOperationId,
                Caption = candidate.Caption, Visibility = candidate.Visibility, CreatedAtUtc = candidate.CreatedAtUtc,
                UpdatedAtUtc = candidate.UpdatedAtUtc
            });
            try
            {
                await db.SaveChangesAsync(token);
            }
            catch (DbUpdateException error) when (error.InnerException is SqlException { Number: 547 } sql &&
                sql.Message.Contains(StopForeignKey, StringComparison.Ordinal))
            {
                throw new TripMemoryConflictException("Trip stop is no longer available.");
            }
            return new TripMemoryStoreCreateResult(candidate, true);
        }, ct);

    public async Task<TripMemory?> FindAsync(Guid tripId, Guid memoryId, CancellationToken ct)
    {
        await using var db = await contexts.CreateDbContextAsync(ct);
        var row = await db.TripMemories.AsNoTracking().SingleOrDefaultAsync(item =>
            item.TripPlanId == tripId && item.MemoryId == memoryId && item.DeletedAtUtc == null, ct);
        return row is null ? null : ToMemory(row);
    }

    public async Task<IReadOnlyList<TripMemory>> ListOwnedAsync(Guid tripId, string ownerId, CancellationToken ct)
    {
        await using var db = await contexts.CreateDbContextAsync(ct);
        var rows = await db.TripMemories.AsNoTracking().Where(row => row.TripPlanId == tripId &&
            row.OwnerUserId == ownerId && row.DeletedAtUtc == null)
            .OrderByDescending(row => row.CreatedAtUtc).ThenBy(row => row.MemoryId).ToListAsync(ct);
        return rows.Where(row => string.Equals(row.OwnerUserId, ownerId, StringComparison.Ordinal)).Select(ToMemory).ToArray();
    }

    public async Task<TripMemoryMediaMetadata?> ReadAttachedMediaAsync(Guid memoryId, string ownerId, CancellationToken ct)
    {
        await using var db = await contexts.CreateDbContextAsync(ct);
        return await (from memory in db.TripMemories
                      join media in db.TripMemoryMedia on memory.MemoryId equals media.MemoryId
                      where memory.MemoryId == memoryId && memory.OwnerUserId == ownerId &&
                            memory.DeletedAtUtc == null && media.State == TripMemoryMediaState.Attached
                      select new TripMemoryMediaMetadata(media.ContentType, media.ContentLength)).SingleOrDefaultAsync(ct);
    }

    public Task<bool> SoftDeleteAsync(Guid tripId, Guid memoryId, string ownerId, DateTimeOffset now, CancellationToken ct) =>
        WriteAsync(async (db, token) =>
        {
            var row = await db.TripMemories.SingleOrDefaultAsync(item => item.TripPlanId == tripId && item.MemoryId == memoryId, token);
            if (row is null || !string.Equals(row.OwnerUserId, ownerId, StringComparison.Ordinal))
                return false;

            if (row.DeletedAtUtc is null)
            {
                var timestamp = now >= row.UpdatedAtUtc ? now : row.UpdatedAtUtc;
                row.UpdatedAtUtc = timestamp;
                row.DeletedAtUtc = timestamp;
                await db.SaveChangesAsync(token);
            }
            return true;
        }, ct);

    private async Task<T> WriteAsync<T>(Func<WayhunterSharedDbContext, CancellationToken, Task<T>> write, CancellationToken ct)
    {
        await using var strategyDb = await contexts.CreateDbContextAsync(ct);
        try
        {
            return await strategyDb.Database.CreateExecutionStrategy().ExecuteAsync(async token =>
            {
                await using var db = await contexts.CreateDbContextAsync(token);
                await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
                var result = await write(db, token);
                await transaction.CommitAsync(token);
                return result;
            }, ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new TripMemoryConflictException("Memory changed during the operation. Retry the request.");
        }
    }

    private static TripMemory ToMemory(TripMemoryRow row) => new(row.MemoryId, row.TripPlanId, row.TripStopId,
        row.OwnerUserId, row.CreateOperationId, row.Caption, row.Visibility, row.CreatedAtUtc, row.UpdatedAtUtc, row.DeletedAtUtc);
}
