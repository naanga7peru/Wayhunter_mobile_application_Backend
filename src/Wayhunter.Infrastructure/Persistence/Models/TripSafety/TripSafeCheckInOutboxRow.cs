using System;

namespace Wayhunter.Infrastructure.Persistence.Models.TripSafety;

public sealed class TripSafeCheckInOutboxRow
{
    public Guid Id { get; set; }
    public Guid CheckInId { get; set; }
    public string RecipientUserId { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public int DeliveryAttempts { get; set; }
    public bool IsDelivered { get; set; }
    public DateTimeOffset? DeliveredAtUtc { get; set; }
    public DateTimeOffset? NextAttemptAtUtc { get; set; }
    public Guid? LeaseToken { get; set; }
    public DateTimeOffset? LeaseUntilUtc { get; set; }
    public DateTimeOffset? DeadLetteredAtUtc { get; set; }
    public string? LastError { get; set; }
}
