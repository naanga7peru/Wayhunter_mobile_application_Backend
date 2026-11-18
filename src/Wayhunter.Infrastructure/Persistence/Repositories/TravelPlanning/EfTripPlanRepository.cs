
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Wayhunter.Application.Modules.TravelPlanning.Exceptions;
using Wayhunter.Application.Modules.TravelPlanning.Interfaces;
using Wayhunter.Domain.Modules.TravelPlanning.Entities;
using Wayhunter.Domain.Modules.TravelPlanning.Enums;
using Wayhunter.Infrastructure.Persistence.DbContexts;

namespace Wayhunter.Infrastructure.Persistence.Repositories.TravelPlanning;

public sealed class EfTripPlanRepository : ITripPlanRepository
{
    private const int SqlServerRowVersionLength = 8;

    private readonly WayhunterSharedDbContext _dbContext;

    public EfTripPlanRepository(
        WayhunterSharedDbContext dbContext)
    {
        _dbContext = dbContext
            ?? throw new ArgumentNullException(nameof(dbContext));
    }

    // ==========================================
    // GET TRIP BY ID
    // ==========================================
    public async Task<TripPlan?> GetByIdAsync(
        Guid tripPlanId,
        CancellationToken cancellationToken = default)
    {
        if (tripPlanId == Guid.Empty)
        {
            throw new ArgumentException(
                "Trip Plan ID is required.",
                nameof(tripPlanId));
        }

        return await _dbContext.Trips
            .AsTracking()
            .Include(trip => trip.Stops)
            .SingleOrDefaultAsync(
                trip => trip.Id == tripPlanId,
                cancellationToken);
    }

    // ==========================================
    // GET MY TRIP PLANS
    // ==========================================
    public async Task<IReadOnlyList<TripPlan>> GetByOwnerAsync(
        string ownerUserId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ownerUserId))
        {
            throw new ArgumentException(
                "Owner User ID is required.",
                nameof(ownerUserId));
        }

        return await _dbContext.Trips
            .AsNoTracking()
            .Include(trip => trip.Stops)
            .Where(trip => trip.OwnerUserId == ownerUserId)
            .OrderByDescending(trip => trip.StartDate)
            .ToListAsync(cancellationToken);
    }

    // ==========================================
    // CREATE TRIP PLAN
    // ==========================================
    public async Task AddAsync(
        TripPlan tripPlan,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tripPlan);

        await _dbContext.Trips.AddAsync(
            tripPlan,
            cancellationToken);

        await _dbContext.SaveChangesAsync(
            cancellationToken);
    }

    // ==========================================
    // UPDATE TRIP PLAN
    // ==========================================
    public async Task UpdateAsync(
        TripPlan tripPlan,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tripPlan);

        var entry = _dbContext.Entry(tripPlan);

        if (entry.State == EntityState.Detached)
        {
            _dbContext.Trips.Update(tripPlan);
        }

        await _dbContext.SaveChangesAsync(
            cancellationToken);
    }

    // ==========================================
    // UPDATE WITH SQL ROWVERSION PROTECTION
    // Used by Trip Update and Trip Stop Add
    // ==========================================
    public async Task UpdateWithConcurrencyAsync(
        TripPlan tripPlan,
        byte[] expectedRowVersion,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tripPlan);
        ArgumentNullException.ThrowIfNull(expectedRowVersion);

        if (expectedRowVersion.Length != SqlServerRowVersionLength)
        {
            throw new ArgumentException(
                "A valid 8-byte RowVersion is required.",
                nameof(expectedRowVersion));
        }

        var entry = _dbContext.Entry(tripPlan);

        if (entry.State == EntityState.Detached)
        {
            _dbContext.Trips.Attach(tripPlan);

            entry = _dbContext.Entry(tripPlan);

            entry.State = EntityState.Modified;
        }

        // Use the client's original RowVersion
        // to detect concurrent changes.
        entry.Property(trip => trip.RowVersion).OriginalValue =
            expectedRowVersion.ToArray();

        // SQL Server generates RowVersion automatically.
        // Never send RowVersion as a modified column.
        entry.Property(trip => trip.RowVersion).IsModified = false;

        // Force parent UPDATE so SQL Server checks
        // and advances RowVersion for child changes.
        entry.Property(trip => trip.TripName).IsModified = true;

        try
        {
            await _dbContext.SaveChangesAsync(
                cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new TripPlanConcurrencyException(
                tripPlan.Id);
        }
    }

    // ==========================================
    // REMOVE TRIP STOP WITH SQL CONCURRENCY
    // ==========================================
    public async Task<TripPlan> RemoveStopWithConcurrencyAsync(
        TripPlan tripPlan,
        Guid stopId,
        byte[]? expectedRowVersion,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tripPlan);

        cancellationToken.ThrowIfCancellationRequested();

        if (tripPlan.Id == Guid.Empty || stopId == Guid.Empty)
        {
            throw new ArgumentException(
                "Valid Trip Plan ID and Stop ID are required.");
        }

        // STEP 1 - Validate RowVersion.
        if (expectedRowVersion is null ||
            expectedRowVersion.Length != SqlServerRowVersionLength)
        {
            throw new ArgumentException(
                "An 8-byte RowVersion is required.");
        }

        if (tripPlan.RowVersion is null ||
            !tripPlan.RowVersion.SequenceEqual(expectedRowVersion))
        {
            throw new TripPlanConcurrencyException(tripPlan.Id);
        }

        // STEP 2 - Check editable status.
        if (tripPlan.DraftStatus != TripDraftStatus.Draft)
        {
            throw new InvalidOperationException(
                "Only draft trip plans can be modified.");
        }

        // STEP 3 - Find requested stop.
        var removedStop = tripPlan.Stops
            .FirstOrDefault(stop => stop.Id == stopId);

        if (removedStop is null)
        {
            throw new KeyNotFoundException(
                "Trip stop not found.");
        }

        var removedOrder = removedStop.OrderIndex;

        var maxOrder = tripPlan.Stops
            .Max(stop => stop.OrderIndex);

        // STEP 4 - Prevent integer overflow.
        if (maxOrder < 0 ||
            ((long)maxOrder * 2 + 1) > int.MaxValue)
        {
            throw new InvalidOperationException(
                "Trip stop ordering exceeds supported limits.");
        }

        var offset = maxOrder + 1;

        // STEP 5 - Begin SQL transaction.
        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        // STEP 6 - Validate parent RowVersion.
        var draftStatus = (int)TripDraftStatus.Draft;

        var updated = await _dbContext.Database
            .ExecuteSqlInterpolatedAsync(
                $@"UPDATE [Trips]
                   SET [TripName] = [TripName]
                   WHERE [Id] = {tripPlan.Id}
                     AND [OwnerUserId] = {tripPlan.OwnerUserId}
                     AND [RowVersion] = {expectedRowVersion}
                     AND [DraftStatus] = {draftStatus}",
                cancellationToken);

        if (updated != 1)
        {
            throw new TripPlanConcurrencyException(
                tripPlan.Id);
        }

        // STEP 7 - Delete requested stop.
        var deleted = await _dbContext.Database
            .ExecuteSqlInterpolatedAsync(
                $@"DELETE FROM [TripStops]
                   WHERE [Id] = {stopId}
                     AND [TripPlanId] = {tripPlan.Id}",
                cancellationToken);

        if (deleted != 1)
        {
            throw new TripPlanConcurrencyException(
                tripPlan.Id);
        }

        // STEP 8 - Reorder remaining stops.
        // Unique index: (TripPlanId, OrderIndex)
        if (removedOrder < maxOrder)
        {
            // Phase A - Move following stops
            // outside the existing index range.
            await _dbContext.Database
                .ExecuteSqlInterpolatedAsync(
                    $@"UPDATE [TripStops]
                       SET [OrderIndex] =
                           [OrderIndex] + {offset}
                       WHERE [TripPlanId] = {tripPlan.Id}
                         AND [OrderIndex] > {removedOrder}",
                    cancellationToken);

            // Phase B - Normalize indexes.
            await _dbContext.Database
                .ExecuteSqlInterpolatedAsync(
                    $@"UPDATE [TripStops]
                       SET [OrderIndex] =
                           [OrderIndex] - {offset} - 1
                       WHERE [TripPlanId] = {tripPlan.Id}
                         AND [OrderIndex] >= {offset}",
                    cancellationToken);
        }

        // STEP 9 - Commit transaction.
        await transaction.CommitAsync(cancellationToken);

        // STEP 10 - Clear tracked SQL state.
        _dbContext.ChangeTracker.Clear();

        // STEP 11 - Reload fresh trip.
        return await GetByIdAsync(
            tripPlan.Id,
            cancellationToken)
            ?? throw new InvalidOperationException(
                "Updated trip plan could not be reloaded.");
    }

    // ==========================================
    // NEW: REORDER TRIP STOPS WITH SQL CONCURRENCY
    // ==========================================
    public async Task<TripPlan> ReorderStopsWithConcurrencyAsync(
        TripPlan tripPlan,
        IReadOnlyList<Guid> orderedStopIds,
        byte[]? expectedRowVersion,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tripPlan);
        ArgumentNullException.ThrowIfNull(orderedStopIds);

        cancellationToken.ThrowIfCancellationRequested();

        // STEP 1 - Validate RowVersion.
        if (expectedRowVersion is null ||
            expectedRowVersion.Length != SqlServerRowVersionLength)
        {
            throw new ArgumentException(
                "An 8-byte RowVersion is required.");
        }

        if (tripPlan.RowVersion is null ||
            !tripPlan.RowVersion.SequenceEqual(expectedRowVersion))
        {
            throw new TripPlanConcurrencyException(
                tripPlan.Id);
        }

        // STEP 2 - Only draft trips can be reordered.
        if (tripPlan.DraftStatus != TripDraftStatus.Draft)
        {
            throw new InvalidOperationException(
                "Only draft trip plans can be reordered.");
        }

        // STEP 3 - Load current stop ordering.
        var existingStops = tripPlan.Stops
            .OrderBy(stop => stop.OrderIndex)
            .ToList();

        // STEP 4 - Validate all stop IDs.
        if (orderedStopIds.Count != existingStops.Count ||
            orderedStopIds.Distinct().Count() !=
                orderedStopIds.Count)
        {
            throw new ArgumentException(
                "Every trip stop must appear exactly once.");
        }

        var existingIds = existingStops
            .Select(stop => stop.Id)
            .ToHashSet();

        if (orderedStopIds.Any(
            stopId => !existingIds.Contains(stopId)))
        {
            throw new ArgumentException(
                "Invalid or foreign trip stop ID.");
        }

        // STEP 5 - Skip unchanged order.
        var currentOrder = existingStops
            .Select(stop => stop.Id);

        if (currentOrder.SequenceEqual(orderedStopIds))
        {
            return tripPlan;
        }

        // STEP 6 - Calculate temporary unique indexes.
        var maxOrder = existingStops
            .Max(stop => stop.OrderIndex);

        if (existingStops.Any(
                stop => stop.OrderIndex < 0) ||
            maxOrder > int.MaxValue - existingStops.Count)
        {
            throw new InvalidOperationException(
                "Trip stop order exceeds supported limits.");
        }

        var temporaryStart = maxOrder + 1;

        var temporaryIndexes =
            new Dictionary<Guid, int>();

        // STEP 7 - Begin atomic SQL transaction.
        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        // STEP 8 - Check parent RowVersion and lock trip.
        var draftStatus = (int)TripDraftStatus.Draft;

        var updated = await _dbContext.Database
            .ExecuteSqlInterpolatedAsync(
                $@"UPDATE [Trips]
                   SET [TripName] = [TripName]
                   WHERE [Id] = {tripPlan.Id}
                     AND [OwnerUserId] = {tripPlan.OwnerUserId}
                     AND [RowVersion] = {expectedRowVersion}
                     AND [DraftStatus] = {draftStatus}",
                cancellationToken);

        if (updated != 1)
        {
            throw new TripPlanConcurrencyException(
                tripPlan.Id);
        }

        // STEP 9 - Move stops to temporary indexes.
        // Each update targets one row, avoiding
        // intermediate unique-index collisions.
        for (var index = 0;
             index < existingStops.Count;
             index++)
        {
            var stop = existingStops[index];

            var temporaryIndex =
                temporaryStart + index;

            var affected = await _dbContext.Database
                .ExecuteSqlInterpolatedAsync(
                    $@"UPDATE [TripStops]
                       SET [OrderIndex] = {temporaryIndex}
                       WHERE [Id] = {stop.Id}
                         AND [TripPlanId] = {tripPlan.Id}
                         AND [OrderIndex] = {stop.OrderIndex}",
                    cancellationToken);

            if (affected != 1)
            {
                throw new TripPlanConcurrencyException(
                    tripPlan.Id);
            }

            temporaryIndexes.Add(
                stop.Id,
                temporaryIndex);
        }

        // STEP 10 - Assign final indexes 0, 1, 2...
        for (var index = 0;
             index < orderedStopIds.Count;
             index++)
        {
            var stopId = orderedStopIds[index];

            var temporaryIndex =
                temporaryIndexes[stopId];

            var affected = await _dbContext.Database
                .ExecuteSqlInterpolatedAsync(
                    $@"UPDATE [TripStops]
                       SET [OrderIndex] = {index}
                       WHERE [Id] = {stopId}
                         AND [TripPlanId] = {tripPlan.Id}
                         AND [OrderIndex] = {temporaryIndex}",
                    cancellationToken);

            if (affected != 1)
            {
                throw new TripPlanConcurrencyException(
                    tripPlan.Id);
            }
        }

        // STEP 11 - Commit all changes together.
        await transaction.CommitAsync(cancellationToken);

        // STEP 12 - Detach affected tracked stops.
        // Avoid clearing unrelated tracked entities.
        foreach (var stop in existingStops)
        {
            _dbContext.Entry(stop).State =
                EntityState.Detached;
        }

        _dbContext.Entry(tripPlan).State =
            EntityState.Detached;

        // STEP 13 - Reload latest stops and RowVersion.
        return await GetByIdAsync(
            tripPlan.Id,
            cancellationToken)
            ?? throw new InvalidOperationException(
                "Updated trip plan could not be reloaded.");
    }
}
