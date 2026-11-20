namespace Wayhunter.Infrastructure.Persistence.Models.HomeAdsAnalytics;

/// <summary>
/// Proposed M7 audit event storage row.
/// NOT registered in the shared DbContext.
/// Audit immutability must be enforced by the final write repository
/// and database access policy, not by this DTO alone.
/// </summary>
public sealed class AdvertisementImageUploadAuditEventRow
{
    public Guid EventId { get; set; }
    public Guid UploadOperationId { get; set; }

    public string EventType { get; set; } = string.Empty;

    public DateTimeOffset TimestampUtc { get; set; }

    public string ActorKind { get; set; } = string.Empty;
    public string ActorId { get; set; } = string.Empty;

    // Only sanitized machine-readable codes, never raw exceptions.
    public string? ErrorCode { get; set; }

    public string? CorrelationId { get; set; }

    public int? PreviousState { get; set; }
    public int? NewState { get; set; }
}