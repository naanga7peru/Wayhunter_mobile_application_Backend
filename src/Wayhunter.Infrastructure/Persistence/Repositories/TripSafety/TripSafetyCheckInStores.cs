using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.TripSafety.Interfaces;
namespace Wayhunter.Infrastructure.Persistence.Repositories.TripSafety;
public sealed class InMemoryTripSafetyCheckInStore : ITripSafetyCheckInStore
{
    private readonly object _sync = new();
    private readonly Dictionary<(Guid SessionId, string UserId), TripSafeCheckIn> _checkIns = new();
    public Task<TripSafeCheckInWriteResult> RecordAsync(TripSafeCheckIn checkIn, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(checkIn);
        if (checkIn.CheckInId == Guid.Empty || checkIn.TripPlanId == Guid.Empty || checkIn.SessionId == Guid.Empty || string.IsNullOrWhiteSpace(checkIn.UserId) || checkIn.CheckedInAtUtc == default || checkIn.CheckedInAtUtc.Offset != TimeSpan.Zero) throw new ArgumentException("Invalid safe check-in data.");
        lock (_sync)
        {
            var key = (checkIn.SessionId, checkIn.UserId);
            if (_checkIns.TryGetValue(key, out var existing))
            {
                if (existing.TripPlanId != checkIn.TripPlanId) throw new InvalidOperationException("Check-in session belongs to another trip.");
                return Task.FromResult(new TripSafeCheckInWriteResult(existing, false));
            }
            _checkIns.Add(key, checkIn);
            return Task.FromResult(new TripSafeCheckInWriteResult(checkIn, true));
        }
    }
}
public sealed class UnavailableTripSafetyCheckInStore : ITripSafetyCheckInStore
{
    public Task<TripSafeCheckInWriteResult> RecordAsync(TripSafeCheckIn checkIn, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        throw new TripSafetyCheckInUnavailableException("SQL safe check-in persistence and outbox are not configured.");
    }
}
