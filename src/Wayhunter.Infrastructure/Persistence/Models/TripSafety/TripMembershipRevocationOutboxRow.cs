
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
}
