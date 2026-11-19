using System;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Wayhunter.Application.Modules.TravelPlanning.Interfaces;
using Wayhunter.Domain.Modules.TravelPlanning.Entities;
using Wayhunter.Domain.Modules.TravelPlanning.Enums;
using Wayhunter.Domain.Modules.TripSafety.Enums;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Wayhunter.Infrastructure.Persistence.Models.TravelPlanning;

namespace Wayhunter.Infrastructure.Persistence.Repositories.TravelPlanning;

// This implementation MUST NOT be enabled until the team approves that
// active M3 membership grants reuse of planned stops AND the schema change.
// The DI flag defaults to false, so production/demo paths fail closed.
public sealed class SqlCopySuggestedTripPlanStore : ICopySuggestedTripPlanStore
{
    private readonly WayhunterSharedDbContext _db;

    public SqlCopySuggestedTripPlanStore(WayhunterSharedDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<TripPlan?> CopyEligibleAsync(
        Guid sourceTripPlanId,
        string authenticatedUserId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (sourceTripPlanId == Guid.Empty)
            throw new ArgumentException("Valid source trip ID is required.", nameof(sourceTripPlanId));
        ArgumentException.ThrowIfNullOrWhiteSpace(authenticatedUserId);

        // A retrying SQL execution strategy requires the transaction to be
        // inside its delegate. SERIALIZABLE guards against membership
        // removal/completion changes during authorization and insertion.
        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            // Each retry starts with an untracked source and a fresh draft.
            _db.ChangeTracker.Clear();
            await using var transaction = await _db.Database
                .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

            var source = await _db.Trips
                .AsNoTracking()
                .Include(trip => trip.Stops)
                .SingleOrDefaultAsync(trip =>
                    trip.Id == sourceTripPlanId &&
                    trip.OwnerUserId != authenticatedUserId &&
                    trip.DraftStatus != TripDraftStatus.Cancelled &&
                    _db.TripMembers.Any(member =>
                        member.TripPlanId == trip.Id &&
                        member.MemberUserId == authenticatedUserId &&
                        member.IsActive && member.RemovedAtUtc == null) &&
                    _db.LiveTripSessions.Any(session =>
                        session.TripPlanId == trip.Id &&
                        session.Status == LiveTripStatus.Finished &&
                        session.FinishedAtUtc.HasValue &&
                        session.FinishedAtUtc.Value >= session.StartedAtUtc) &&
                    !_db.LiveTripSessions.Any(session =>
                        session.TripPlanId == trip.Id &&
                        session.Status != LiveTripStatus.Finished),
                    cancellationToken);

            if (source is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return (TripPlan?)null;
            }

            // All source coordinates are PLANNED stop positions; no M3
            // samples, live routes, friends or memories are queried/copied.
            var draft = TripPlan.CreateCopyFrom(source, authenticatedUserId);
            await _db.Trips.AddAsync(draft, cancellationToken);
            _db.Set<TripPlanCopyAttributionRow>().Add(new TripPlanCopyAttributionRow
            {
                TripPlanId = draft.Id,
                SourceTripPlanId = source.Id,
                SourceOwnerUserId = source.OwnerUserId,
                CopiedAtUtc = DateTimeOffset.UtcNow
            });
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return draft;
        });
    }
}
