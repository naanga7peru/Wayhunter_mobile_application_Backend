using Wayhunter.Application.Modules.TripSafety.Notifications;

namespace Wayhunter.Infrastructure.Persistence.Models.TripSafety;

public sealed class NotificationEventRow
{
    public Guid EventId { get; set; }
    public NotificationCategory Category { get; set; }
    public NotificationTemplate Template { get; set; }
    public int TemplateVersion { get; set; }
    public Guid? TargetId { get; set; }
    public Guid? JobId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed class NotificationRecipientRow
{
    public Guid EventId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public DateTimeOffset? ReadAtUtc { get; set; }
    public NotificationDeliveryState State { get; set; }
    public int Attempts { get; set; }
    public DateTimeOffset? NextAttemptAtUtc { get; set; }
    public Guid? LeaseToken { get; set; }
    public DateTimeOffset? LeaseUntilUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public string? LastOutcome { get; set; }
}

public sealed class NotificationPreferenceRow
{
    public string UserId { get; set; } = string.Empty;
    public NotificationCategory Category { get; set; }
    public bool PushEnabled { get; set; }
}

public sealed class NotificationDeviceRow
{
    public Guid DeviceId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string TokenFingerprint { get; set; } = string.Empty;
    public byte[] ProtectedToken { get; set; } = [];
    public bool PushAllowed { get; set; }
    public DateTimeOffset ChangedAtUtc { get; set; }
    public DateTimeOffset? RevokedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

public sealed class NotificationTokenBindingRow
{
    public string Fingerprint { get; set; } = string.Empty;
    public Guid DeviceId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public DateTimeOffset RecordedAtUtc { get; set; }
}

public sealed class NotificationJobRow
{
    public Guid JobId { get; set; }
    public string ActorId { get; set; } = string.Empty;
    public NotificationJobKind Kind { get; set; }
    public NotificationJobState State { get; set; }
    public int Revision { get; set; }
    public DateTimeOffset DueAtUtc { get; set; }
    public NotificationTemplate Template { get; set; }
    public int TemplateVersion { get; set; }
    public Guid? TargetId { get; set; }
    public Guid? TripPlanId { get; set; }
    public Guid? SessionId { get; set; }
    public DateOnly? PlannedStartDate { get; set; }
    public string? TripVersion { get; set; }
    public NotificationAudienceRole? AudienceRole { get; set; }
    public string? AudienceDistrict { get; set; }
    public string? AudienceFingerprint { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

public sealed class NotificationJobRecipientRow
{
    public Guid JobId { get; set; }
    public string UserId { get; set; } = string.Empty;
}

public sealed class NotificationAudienceReceiptRow
{
    public Guid ReceiptId { get; set; }
    public string ActorId { get; set; } = string.Empty;
    public string Fingerprint { get; set; } = string.Empty;
    public int Count { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset? ConsumedAtUtc { get; set; }
}

public sealed class NotificationOperationRow
{
    public string ActorId { get; set; } = string.Empty;
    public Guid OperationId { get; set; }
    public Guid ResourceId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Fingerprint { get; set; } = string.Empty;
    public NotificationJobState? JobState { get; set; }
    public int? Revision { get; set; }
    public DateTimeOffset? DueAtUtc { get; set; }
    public DateTimeOffset RecordedAtUtc { get; set; }
}

public sealed class NotificationDeliveryAuditRow
{
    public Guid AuditId { get; set; }
    public Guid EventId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int Attempt { get; set; }
    public string Outcome { get; set; } = string.Empty;
    public DateTimeOffset RecordedAtUtc { get; set; }
}
