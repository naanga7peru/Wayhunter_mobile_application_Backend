using System;
using System.Threading;
using System.Threading.Tasks;

namespace Wayhunter.Application.Modules.TripSafety.Interfaces;

public sealed record TripSafeCheckIn(Guid CheckInId, Guid TripPlanId, Guid SessionId, string UserId, DateTimeOffset CheckedInAtUtc);

public sealed record TripSafeCheckInWriteResult(TripSafeCheckIn CheckIn, bool Created);

public interface ITripSafetyCheckInStore
{
    Task<TripSafeCheckInWriteResult> RecordAsync(TripSafeCheckIn checkIn, CancellationToken cancellationToken = default);
}

public sealed class TripSafetyCheckInUnavailableException : Exception
{
    public TripSafetyCheckInUnavailableException(string message) : base(message) { }
}
