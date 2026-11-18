
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Wayhunter.Application.Modules.TravelPlanning.Exceptions;
using Wayhunter.Application.Modules.TravelPlanning.Interfaces;
using Wayhunter.Domain.Modules.TravelPlanning.Entities;
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
    // Used by Trip Update and Trip Stop Add.
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

        // ==========================================
        // NEW: TRIP STOP CONCURRENCY PROTECTION
        // ==========================================
        // Adding a TripStop may only change the child table.
        //
        // Force an UPDATE on the parent Trips row so that
        // SQL Server checks the expected RowVersion and
        // generates a fresh RowVersion.
        //
        // This also protects against concurrent stop edits.
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
}
