
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

        // Use the client's original token for the SQL UPDATE predicate.
        // SQL Server will reject the update if the stored RowVersion
        // no longer matches this value.
        entry.Property(trip => trip.RowVersion).OriginalValue =
            expectedRowVersion.ToArray();

        entry.Property(trip => trip.RowVersion).IsModified = false;

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
