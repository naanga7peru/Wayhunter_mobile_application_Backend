using Wayhunter.Application.Modules.TripSafety.Memories;

namespace Wayhunter.Infrastructure.Persistence.Models.TripSafety;

public sealed class TripMemoryRow
{
    public Guid MemoryId { get; set; }
    public Guid TripPlanId { get; set; }
    public Guid? TripStopId { get; set; }
    public string OwnerUserId { get; set; } = string.Empty;
    public Guid CreateOperationId { get; set; }
    public string Caption { get; set; } = string.Empty;
    public TripMemoryVisibility Visibility { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public DateTimeOffset? DeletedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

public sealed class TripMemoryMediaRow
{
    public Guid OperationId { get; set; }
    public Guid MemoryId { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public long ContentLength { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public TripMemoryMediaState State { get; set; }
    public string? ObjectKey { get; set; }
    public string? ObjectVersion { get; set; }
    public Guid? CleanupClaimId { get; set; }
    public DateTimeOffset? CleanedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
