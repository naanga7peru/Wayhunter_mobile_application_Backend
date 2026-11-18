
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

        // Force a parent trip UPDATE for child stop changes.
        // SQL Server checks and advances the RowVersion.
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
    // NEW: REMOVE TRIP STOP WITH SQL CONCURRENCY
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

        // Step 1: Validate SQL RowVersion.
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

        // Step 2: Only draft trips can be modified.
        if (tripPlan.DraftStatus != TripDraftStatus.Draft)
        {
            throw new InvalidOperationException(
                "Only draft trip plans can be modified.");
        }

        // Step 3: Confirm stop belongs to trip.
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

        // Step 4: Prevent overflow when calculating
        // temporary order indexes.
        if (maxOrder < 0 ||
            ((long)maxOrder * 2 + 1) > int.MaxValue)
        {
            throw new InvalidOperationException(
                "Trip stop ordering exceeds supported limits.");
        }

        var offset = maxOrder + 1;

        // Step 5: Begin atomic SQL transaction.
        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        // Step 6: Check and advance parent RowVersion.
        // A zero-row result means stale version,
        // wrong owner or no longer editable trip.
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

        // Step 7: Delete the requested stop.
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

        // Step 8: Reorder remaining stops.
        // Existing unique index:
        // (TripPlanId, OrderIndex)
        //
        // Use a two-phase shift to avoid
        // temporary unique-index collisions.

        if (removedOrder < maxOrder)
        {
            // Phase A:
            // Move following stops outside their
            // original index range.
            await _dbContext.Database
                .ExecuteSqlInterpolatedAsync(
                    $@"UPDATE [TripStops]
                       SET [OrderIndex] =
                           [OrderIndex] + {offset}
                       WHERE [TripPlanId] = {tripPlan.Id}
                         AND [OrderIndex] > {removedOrder}",
                    cancellationToken);

            // Phase B:
            // Normalize to continuous indexes.
            await _dbContext.Database
                .ExecuteSqlInterpolatedAsync(
                    $@"UPDATE [TripStops]
                       SET [OrderIndex] =
                           [OrderIndex] - {offset} - 1
                       WHERE [TripPlanId] = {tripPlan.Id}
                         AND [OrderIndex] >= {offset}",
                    cancellationToken);
        }

        // Step 9: Commit all changes together.
        await transaction.CommitAsync(cancellationToken);

        // Step 10:
        // Raw SQL bypassed EF tracked entities.
        // Clear tracked state to avoid stale results.
        _dbContext.ChangeTracker.Clear();

        // Step 11:
        // Reload fresh stops and new RowVersion.
        return await GetByIdAsync(
            tripPlan.Id,
            cancellationToken)
            ?? throw new InvalidOperationException(
                "Updated trip plan could not be reloaded.");
    }
}
