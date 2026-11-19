
using System;

namespace Wayhunter.Infrastructure.Persistence.Models.TripSafety;

public sealed class TripMembershipRevocationOutboxRow
{
    public Guid Id { get; set; }

    public Guid TripPlanId { get; set; }

    public Guid MemberId { get; set; }

    public string MemberUserId { get; set; } = string.Empty;

    public DateTimeOffset RemovedAtUtc { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public bool IsDelivered { get; set; }

    public DateTimeOffset? DeliveredAtUtc { get; set; }

    public int DeliveryAttempts { get; set; }

    // Null means immediately eligible for first delivery.
    public DateTimeOffset? NextAttemptAtUtc { get; set; }

    // Atomic database claim identity.
    public Guid? LeaseToken { get; set; }

    // Allows recovery if a worker crashes.
    public DateTimeOffset? LeaseUntilUtc { get; set; }

    // Terminal failure; record is retained for audit.
    public DateTimeOffset? DeadLetteredAtUtc { get; set; }

    // Store an exception type only.
    // Never persist provider secrets or response bodies.
    public string? LastError { get; set; }
}
