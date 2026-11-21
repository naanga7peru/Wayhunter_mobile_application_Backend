namespace Wayhunter.Application.Modules.TripSafety.Notifications;

public enum NotificationCategory { Trip = 1, Safety, Social, Hunter, Game, Coins, Voucher }
public enum NotificationTemplate { TripReminder = 1, SafetyUpdate, SocialUpdate, HunterReview, GameReview, CoinsUpdate, VoucherUpdate }
public enum NotificationDeliveryState { Pending = 0, Delivered, Suppressed, DeadLettered }
public enum NotificationJobState { Scheduled = 0, Published, Cancelled }
public enum NotificationJobKind { TripReminder = 1, AdminMessage }
public enum NotificationPushResult { Delivered, NoActiveDevice, InvalidDevice, TemporaryFailure }
public enum NotificationAudienceRole { Member = 1, Administrator }

public sealed record NotificationPreference(NotificationCategory Category, bool PushEnabled);
public sealed record NotificationTarget(NotificationCategory Category, Guid Id);
public sealed record NotificationContent(NotificationCategory Category, NotificationTemplate Template,
    int Version, string PushTitle, string PushBody);
public sealed record NotificationInboxItem(Guid EventId, NotificationCategory Category, NotificationTemplate Template,
    int TemplateVersion, NotificationTarget? Target, DateTimeOffset CreatedAtUtc, DateTimeOffset? ReadAtUtc);
public sealed record NotificationInboxPage(IReadOnlyList<NotificationInboxItem> Items, long UnreadCount);
public sealed record NotificationDeviceReceipt(Guid DeviceId, Guid OperationId, DateTimeOffset ChangedAtUtc);
public sealed record NotificationAudienceFilter(NotificationAudienceRole? Role, string? District, string[]? UserIds);
public sealed record NotificationAudienceMember(string UserId, NotificationAudienceRole Role, string? District);
public sealed record NotificationAudiencePreview(Guid ReceiptId, int Count, DateTimeOffset ExpiresAtUtc);
public sealed record NotificationAdminCommand(Guid OperationId, NotificationTemplate Template, NotificationTarget? Target,
    NotificationAudienceFilter Audience, DateTimeOffset? SendAtUtc, string? Text, string? DeepLink);
public sealed record NotificationJobReceipt(Guid JobId, int Revision, NotificationJobState State, DateTimeOffset DueAtUtc);
public sealed record NotificationDeliverySummary(Guid JobId, int Queued, int Delivered, int Suppressed, int Failed, int Read);
public sealed record NotificationReminderCommand(Guid OperationId, Guid TripPlanId, Guid SessionId,
    DateOnly PlannedStartDate, DateTimeOffset DueAtUtc);
public sealed record NotificationReminder(NotificationReminderCommand Command, string UserId, string TripVersion);
public sealed record NotificationAudienceReceipt(Guid ReceiptId, string ActorId, string Fingerprint,
    int Count, DateTimeOffset ExpiresAtUtc);
public sealed record NotificationAdminSchedule(string ActorId, NotificationAdminCommand Command,
    IReadOnlyList<string> Recipients, string Fingerprint, Guid? PreviewReceiptId, DateTimeOffset Now);

public sealed class NotificationUnavailableException : Exception;
public sealed class NotificationConflictException : Exception;

public interface INotificationInbox
{
    // Scope every query/update with recipientId. Push outcome never determines inbox visibility.
    Task<NotificationInboxPage> ReadAsync(string recipientId, int limit, CancellationToken ct);
    Task<bool> MarkReadAsync(string recipientId, Guid eventId, DateTimeOffset now, CancellationToken ct);
    Task<IReadOnlyList<NotificationPreference>> GetPreferencesAsync(string recipientId, CancellationToken ct);
    Task SetPreferenceAsync(string recipientId, NotificationPreference preference, CancellationToken ct);
}

public interface INotificationDeviceRegistry
{
    // Protect the token with approved encryption and keyed fingerprinting before storage.
    // Verify token/installation proof. Enforce owner, unique fingerprint, operation
    // idempotency and CAS rotation/revocation. Never transfer a token to another owner.
    Task<NotificationDeviceReceipt> RegisterOrRotateAsync(string ownerId, Guid deviceId, Guid operationId,
        string rawToken, bool pushAllowed, CancellationToken ct);
    Task<bool> RevokeAsync(string ownerId, Guid deviceId, Guid operationId, CancellationToken ct);
}

public interface INotificationAudienceDirectory
{
    // Authoritative, verified roles and districts, current eligible account status,
    // preferences and actor's specific composer grant. Return no partial result and
    // throw when any scope cannot be verified, including a requested target.
    Task<IReadOnlyList<NotificationAudienceMember>> ResolveAsync(string actorId,
        NotificationAudienceFilter filter, NotificationCategory category, NotificationTarget? target, CancellationToken ct);
    Task<bool> CanManageAsync(string actorId, CancellationToken ct);
}

public interface INotificationJobs
{
    Task SavePreviewAsync(NotificationAudienceReceipt receipt, CancellationToken ct);

    // Atomically verify and consume a matching unexpired one-use preview for bulk sends.
    // Scope receipt to actor and fingerprint. Retry of an identical operation must return
    // prior result before examining a consumed receipt. Persist plan, audience, and audit.
    Task<NotificationJobReceipt> ScheduleAdminAsync(NotificationAdminSchedule command, CancellationToken ct);

    // Atomically reserve operation identity and a revision for the owner/trip/session.
    // Rescheduling and cancellation CAS the same row. No trip start or state change.
    Task<NotificationJobReceipt> ScheduleReminderAsync(NotificationReminder reminder, CancellationToken ct);
    Task<bool> CancelAsync(NotificationJobKind kind, string ownerId, Guid jobId, int revision,
        Guid operationId, DateTimeOffset now, CancellationToken ct);
    Task<NotificationDeliverySummary?> ReadSummaryAsync(string actorId, Guid jobId, CancellationToken ct);
}

// Issuance must occur only after recipient eligibility and job revision/state are
// verified in the same transaction as inserting the event/recipient inbox rows.
public interface INotificationReminderPublisher
{
    Task<bool> PublishIfCurrentAsync(Guid jobId, int revision, NotificationReminder reminder,
        DateTimeOffset now, CancellationToken ct);
}
