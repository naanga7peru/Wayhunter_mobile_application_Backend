using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Wayhunter.Application.Modules.TravelPlanning.Dtos;
using Wayhunter.Application.Modules.TravelPlanning.Interfaces;
using Wayhunter.Application.Modules.TravelPlanning.Queries;
using Wayhunter.Domain.Modules.TravelPlanning.Enums;
using Wayhunter.Domain.Modules.TripSafety.Enums;
using Wayhunter.Infrastructure.Persistence.DbContexts;

namespace Wayhunter.Infrastructure.Persistence.Repositories.TravelPlanning;

// M2-MS05-T01: authorized-shared completed plans ONLY.
// Public discoveries are intentionally excluded until public visibility
// and re-use consent are specified and approved. No migrations here.
public sealed class SqlSuggestedTripPlansReader : ISuggestedTripPlansReader
{
    private readonly WayhunterSharedDbContext _db;

    public SqlSuggestedTripPlansReader(WayhunterSharedDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<IReadOnlyList<SuggestedTripPlanDto>> GetEligibleAsync(
        string authenticatedUserId,
        int maxResults,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(authenticatedUserId);

        if (maxResults < 1 || maxResults > GetSuggestedTripPlansService.MaxLimit)
        {
            throw new ArgumentOutOfRangeException(nameof(maxResults));
        }

        // The access test runs in the database for EVERY request.
        // A removed member, invitation alone, or another user's plan
        // cannot qualify. M3 is the authority for completion evidence.
        var query = _db.Trips
            .AsNoTracking()
            .Where(trip => trip.OwnerUserId != authenticatedUserId &&
                           trip.DraftStatus != TripDraftStatus.Cancelled)
            .Where(trip => _db.TripMembers.Any(member =>
                member.TripPlanId == trip.Id &&
                member.MemberUserId == authenticatedUserId &&
                member.IsActive &&
                member.RemovedAtUtc == null))
            .Where(trip => _db.LiveTripSessions.Any(session =>
                session.TripPlanId == trip.Id &&
                session.Status == LiveTripStatus.Finished &&
                session.FinishedAtUtc.HasValue &&
                session.FinishedAtUtc.Value >= session.StartedAtUtc))
            // If a trip has been started again, it is not yet finished.
            .Where(trip => !_db.LiveTripSessions.Any(session =>
                session.TripPlanId == trip.Id &&
                session.Status != LiveTripStatus.Finished))
            .Select(trip => new SuggestedTripPlanDto(
                trip.Id,
                trip.TripName,
                trip.StartDate,
                trip.EndDate,
                trip.BudgetLkr,
                trip.VehicleType,
                trip.Headcount,
                _db.TripStops.Count(stop => stop.TripPlanId == trip.Id),
                _db.LiveTripSessions
                    .Where(session =>
                        session.TripPlanId == trip.Id &&
                        session.Status == LiveTripStatus.Finished &&
                        session.FinishedAtUtc.HasValue &&
                        session.FinishedAtUtc.Value >= session.StartedAtUtc)
                    .OrderByDescending(session => session.FinishedAtUtc)
                    .Select(session => session.FinishedAtUtc!.Value)
                    .FirstOrDefault()))
            .OrderByDescending(plan => plan.CompletedAtUtc)
            .ThenBy(plan => plan.TripPlanId)
            .Take(maxResults);

        return await query.ToListAsync(cancellationToken);
    }
}
