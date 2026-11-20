using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Wayhunter.Application.Modules.TripSafety.Interfaces;
using Wayhunter.Domain.Modules.TripSafety.Enums;
using Wayhunter.Infrastructure.Persistence.DbContexts;
namespace Wayhunter.Infrastructure.Persistence.Repositories.TripSafety;

public sealed class EfTripFinishedReminderGuard : ITripFinishedReminderGuard
{
    private readonly WayhunterSharedDbContext _db;

    public EfTripFinishedReminderGuard(WayhunterSharedDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public Task<bool> CanDispatchAsync(
        Guid tripPlanId,
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (tripPlanId == Guid.Empty || sessionId == Guid.Empty)
            throw new ArgumentException("Valid trip and session IDs are required.");

        return _db.LiveTripSessions.AsNoTracking().AnyAsync(
            session => session.TripPlanId == tripPlanId &&
                session.SessionId == sessionId &&
                (session.Status == LiveTripStatus.Active || session.Status == LiveTripStatus.Paused),
            cancellationToken);
    }
}
