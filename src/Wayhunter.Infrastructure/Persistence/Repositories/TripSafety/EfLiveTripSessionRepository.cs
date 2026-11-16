
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Wayhunter.Application.Modules.TripSafety.Interfaces;
using Wayhunter.Domain.Modules.TripSafety.Entities;
using Wayhunter.Domain.Modules.TripSafety.Enums;
using Wayhunter.Infrastructure.Persistence.DbContexts;

namespace Wayhunter.Infrastructure.Persistence.Repositories.TripSafety;

public sealed class EfLiveTripSessionRepository
    : ILiveTripSessionRepository
{
    private readonly WayhunterSharedDbContext _dbContext;

    public EfLiveTripSessionRepository(
        WayhunterSharedDbContext dbContext)
    {
        _dbContext = dbContext
            ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<LiveTripSession?> GetByIdAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return await _dbContext.LiveTripSessions
            .FirstOrDefaultAsync(
                x => x.SessionId == sessionId,
                cancellationToken);
    }

    public async Task<LiveTripSession?> GetOpenByTripPlanIdAsync(
        Guid tripPlanId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (tripPlanId == Guid.Empty)
        {
            throw new ArgumentException(
                "Valid Trip Plan ID is required.",
                nameof(tripPlanId));
        }

        return await _dbContext.LiveTripSessions
            .FirstOrDefaultAsync(
                x => x.TripPlanId == tripPlanId &&
                     x.Status != LiveTripStatus.Finished,
                cancellationToken);
    }

    public async Task AddAsync(
        LiveTripSession session,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(session);

        bool duplicateId = await _dbContext.LiveTripSessions
            .AnyAsync(
                x => x.SessionId == session.SessionId,
                cancellationToken);

        if (duplicateId)
        {
            throw new InvalidOperationException(
                "Session ID already exists.");
        }

        if (session.Status != LiveTripStatus.Finished)
        {
            bool hasOpenSession =
                await _dbContext.LiveTripSessions.AnyAsync(
                    x => x.TripPlanId == session.TripPlanId &&
                         x.Status != LiveTripStatus.Finished,
                    cancellationToken);

            if (hasOpenSession)
            {
                throw new InvalidOperationException(
                    "This trip already has an open session.");
            }
        }

        await _dbContext.LiveTripSessions.AddAsync(
            session,
            cancellationToken);

        await _dbContext.SaveChangesAsync(
            cancellationToken);
    }

    public async Task UpdateAsync(
        LiveTripSession session,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(session);

        bool exists = await _dbContext.LiveTripSessions
            .AnyAsync(
                x => x.SessionId == session.SessionId,
                cancellationToken);

        if (!exists)
        {
            throw new KeyNotFoundException(
                "Live trip session was not found.");
        }

        var tracked = _dbContext.ChangeTracker
            .Entries<LiveTripSession>();

        bool alreadyTracked = false;

        foreach (var entry in tracked)
        {
            if (entry.Entity.SessionId == session.SessionId)
            {
                if (!ReferenceEquals(entry.Entity, session))
                {
                    throw new InvalidOperationException(
                        "A different instance of this session " +
                        "is already tracked by the DbContext.");
                }

                alreadyTracked = true;
                break;
            }
        }

        if (!alreadyTracked)
        {
            _dbContext.LiveTripSessions.Update(session);
        }

        await _dbContext.SaveChangesAsync(
            cancellationToken);
    }
}
