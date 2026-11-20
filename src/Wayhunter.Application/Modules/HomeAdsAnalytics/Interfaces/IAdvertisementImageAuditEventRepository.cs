namespace Wayhunter.Application.Modules.HomeAdsAnalytics.Interfaces;

public enum AdvertisementImageAuditEventType
{
    OperationPrepared,
    ProviderUploadRequested,
    ProviderUploadSucceeded,
    ProviderUploadOutcomeUnknown,
    AdvertisementCasSucceeded,
    AdvertisementCasConflict,
    AdvertisementSqlOutcomeUnknown,
    WorkerClaimAcquired,
    WorkerClaimRejected,
    RetryScheduled,
    RetryExhausted,
    ReferenceVerified,
    CleanupAuthorized,
    CleanupFailed,
    CleanupConfirmed,
    ManualReviewRequired,
    ManualReviewResolved
}

public enum AdvertisementImageAuditActorKind
{
    Administrator,
    RecoveryWorker,
    System
}

/// <summary>
/// Validated audit event with read-only properties.
/// Construct through the trusted server-side factory.
/// </summary>
public sealed class AdvertisementImageAuditEvent
{
    internal AdvertisementImageAuditEvent(
        Guid eventId,
        Guid uploadOperationId,
        string eventType,
        DateTimeOffset timestampUtc,
        string actorKind,
        string actorId,
        string? errorCode,
        string? correlationId,
        AdvertisementImageRecoveryState? previousState,
        AdvertisementImageRecoveryState? newState)
    {
        EventId = eventId;
        UploadOperationId = uploadOperationId;
        EventType = eventType;
        TimestampUtc = timestampUtc;
        ActorKind = actorKind;
        ActorId = actorId;
        ErrorCode = errorCode;
        CorrelationId = correlationId;
        PreviousState = previousState;
        NewState = newState;
    }

    public Guid EventId { get; }
    public Guid UploadOperationId { get; }
    public string EventType { get; }
    public DateTimeOffset TimestampUtc { get; }
    public string ActorKind { get; }
    public string ActorId { get; }
    public string? ErrorCode { get; }
    public string? CorrelationId { get; }
    public AdvertisementImageRecoveryState? PreviousState { get; }
    public AdvertisementImageRecoveryState? NewState { get; }
}

/// <summary>
/// Proposed append-only audit persistence contract.
/// True: committed append confirmed.
/// False: duplicate EventId confirmed without a new append.
/// SQL uncertainty must throw, never return false.
///
/// The final implementation must enforce the unique EventId and
/// use the same SQL transaction as the relevant state change.
/// No runtime implementation is registered yet.
/// </summary>
public interface IAdvertisementImageAuditEventRepository
{
    Task<bool> TryAppendAsync(
        AdvertisementImageAuditEvent auditEvent,
        CancellationToken cancellationToken = default);
}