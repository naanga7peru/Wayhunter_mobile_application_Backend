using System;

namespace Wayhunter.Infrastructure.Persistence.Models.TripSafety;

public sealed class TripSafeCheckInRow
{
    public Guid CheckInId { get; set; }
    public Guid TripPlanId { get; set; }
    public Guid SessionId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public DateTimeOffset CheckedInAtUtc { get; set; }
}
