using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Wayhunter.Application.Modules.TripSafety.Interfaces;
using Wayhunter.Domain.Modules.TripSafety.Enums;
using Wayhunter.Infrastructure.Persistence.DbContexts;
namespace Wayhunter.Infrastructure.Persistence.Repositories.TripSafety;
public sealed class InMemoryTripFinishedReminderGuard : ITripFinishedReminderGuard
{
    private readonly object _sync = new();
    private readonly HashSet<Guid> _finishedTrips = new();
    public Task MarkFinishedAsync(Guid tripPlanId, Guid sessionId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (tripPlanId == Guid.Empty || sessionId == Guid.Empty) throw new ArgumentException("Valid trip and session IDs are required.");
        lock (_sync) { _finishedTrips.Add(tripPlanId); }
        return Task.CompletedTask;
    }
    public Task<bool> HasFinishedAsync(Guid tripPlanId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (tripPlanId == Guid.Empty) throw new ArgumentException("Valid trip plan ID is required.");
        lock (_sync) { return Task.FromResult(_finishedTrips.Contains(tripPlanId)); }
    }
}
public sealed class EfTripFinishedReminderGuard : ITripFinishedReminderGuard
{
    private readonly WayhunterSharedDbContext _db;
    public EfTripFinishedReminderGuard(WayhunterSharedDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }
    public async Task MarkFinishedAsync(Guid tripPlanId, Guid sessionId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (tripPlanId == Guid.Empty || sessionId == Guid.Empty) throw new ArgumentException("Valid trip and session IDs are required.");
        var finished = await _db.LiveTripSessions.AsNoTracking().AnyAsync(x => x.TripPlanId == tripPlanId && x.SessionId == sessionId && x.Status == LiveTripStatus.Finished, cancellationToken);
        if (!finished) throw new InvalidOperationException("Trip finish must be persisted first.");
    }
    public async Task<bool> HasFinishedAsync(Guid tripPlanId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (tripPlanId == Guid.Empty) throw new ArgumentException("Valid trip plan ID is required.");
        return await _db.LiveTripSessions.AsNoTracking().AnyAsync(x => x.TripPlanId == tripPlanId && x.Status == LiveTripStatus.Finished, cancellationToken);
    }
}
