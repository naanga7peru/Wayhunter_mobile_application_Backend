using System;
using System.Data;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Wayhunter.Application.Modules.TravelPlanning.Interfaces;
using Wayhunter.Domain.Modules.TravelPlanning.Entities;
using Wayhunter.Domain.Modules.TravelPlanning.Enums;
using Wayhunter.Domain.Modules.TripSafety.Enums;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Wayhunter.Infrastructure.Persistence.Models.TravelPlanning;

namespace Wayhunter.Infrastructure.Persistence.Repositories.TravelPlanning;

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
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (sourceTripPlanId == Guid.Empty)
            throw new ArgumentException("Valid source trip ID is required.", nameof(sourceTripPlanId));
        ArgumentException.ThrowIfNullOrWhiteSpace(authenticatedUserId);

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(idempotencyKey)));
        var requests = _db.Set<TripPlanCopyAttributionRow>().AsNoTracking()
            .Where(row => row.RequesterUserId == authenticatedUserId && row.RequestKeyHash == hash);
        Guid? attemptedDraftId = null;
        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteInTransactionAsync(async token =>
        {
            _db.ChangeTracker.Clear();
            attemptedDraftId = null;

            var source = await _db.Trips
                .AsNoTracking()
                .Include(trip => trip.Stops)
                .SingleOrDefaultAsync(trip =>
                    trip.Id == sourceTripPlanId &&
                    trip.OwnerUserId != authenticatedUserId &&
                    trip.AllowsSuggestedPlanReuse &&
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
                    token);

            var existing = await requests.SingleOrDefaultAsync(token);
            if (existing is not null && existing.SourceTripPlanId != sourceTripPlanId)
                throw new CopyRequestConflictException();
            if (source is null || source.Stops.Count == 0)
                return null;
            if (existing is not null)
            {
                var previous = await _db.Trips.AsNoTracking().Include(trip => trip.Stops)
                    .SingleAsync(trip => trip.Id == existing.TripPlanId &&
                        trip.OwnerUserId == authenticatedUserId, token);
                previous.RestoreCopyAttribution(existing.SourceTripPlanId, existing.SourceOwnerUserId);
                return previous;
            }

            // All source coordinates are PLANNED stop positions; no M3
            // samples, live routes, friends or memories are queried/copied.
            var draft = TripPlan.CreateCopyFrom(source, authenticatedUserId);
            _db.Trips.Add(draft);
            _db.Set<TripPlanCopyAttributionRow>().Add(new TripPlanCopyAttributionRow
            {
                TripPlanId = draft.Id,
                SourceTripPlanId = source.Id,
                SourceOwnerUserId = source.OwnerUserId,
                RequesterUserId = authenticatedUserId,
                RequestKeyHash = hash,
                CopiedAtUtc = DateTimeOffset.UtcNow
            });
            await _db.SaveChangesAsync(token);
            attemptedDraftId = draft.Id;
            return (TripPlan?)draft;
        }, async token => attemptedDraftId.HasValue && await requests.AnyAsync(
            row => row.TripPlanId == attemptedDraftId.Value, token),
            IsolationLevel.Serializable, cancellationToken);
    }

    public async Task<bool> SetReuseConsentAsync(Guid sourceTripPlanId, string ownerUserId,
        bool allowed, CancellationToken cancellationToken)
    {
        // Write one owner-scoped row. SQL serializes this update against copy authorization.
        return await _db.Trips.Where(trip => trip.Id == sourceTripPlanId &&
                trip.OwnerUserId == ownerUserId && trip.DraftStatus != TripDraftStatus.Cancelled)
            .ExecuteUpdateAsync(setters => setters.SetProperty(trip => trip.AllowsSuggestedPlanReuse, allowed),
                cancellationToken) == 1;
    }
}
