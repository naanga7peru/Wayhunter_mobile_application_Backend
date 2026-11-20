using Wayhunter.Application.Modules.HomeAdsAnalytics.Interfaces;

namespace Wayhunter.Application.Modules.HomeAdsAnalytics.Services;

/// <summary>
/// Creates validated audit events from trusted server-side input.
/// Never pass raw exceptions, credentials or client-supplied actor IDs.
/// No persistence or external calls are performed.
/// </summary>
public static class AdvertisementImageAuditEventFactory
{
    public static AdvertisementImageAuditEvent Create(
        Guid eventId,
        Guid uploadOperationId,
        AdvertisementImageAuditEventType eventType,
        DateTimeOffset timestampUtc,
        AdvertisementImageAuditActorKind actorKind,
        string actorId,
        string? errorCode = null,
        string? correlationId = null,
        AdvertisementImageRecoveryState? previousState = null,
        AdvertisementImageRecoveryState? newState = null)
    {
        if (eventId == Guid.Empty || uploadOperationId == Guid.Empty)
            throw new ArgumentException("Invalid audit identity.");

        if (!Enum.IsDefined(typeof(AdvertisementImageAuditEventType),
                eventType))
            throw new ArgumentException("Unsupported audit event type.");

        if (!Enum.IsDefined(typeof(AdvertisementImageAuditActorKind),
                actorKind))
            throw new ArgumentException("Unsupported audit actor kind.");

        if (timestampUtc == default ||
            timestampUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("UTC audit timestamp is required.");

        if (!IsSafeIdentifier(actorId, 128, allowAt: true))
            throw new ArgumentException("Invalid trusted actor identifier.");

        if (errorCode is not null && !IsSafeErrorCode(errorCode))
            throw new ArgumentException("Invalid sanitized audit error code.");

        if (correlationId is not null &&
            !IsSafeIdentifier(correlationId, 128, allowAt: false))
            throw new ArgumentException("Invalid audit correlation ID.");

        if (previousState is { } previous &&
            !Enum.IsDefined(typeof(AdvertisementImageRecoveryState),
                previous))
            throw new ArgumentException("Invalid previous recovery state.");

        if (newState is { } next &&
            !Enum.IsDefined(typeof(AdvertisementImageRecoveryState),
                next))
            throw new ArgumentException("Invalid new recovery state.");

        return new AdvertisementImageAuditEvent(
            eventId,
            uploadOperationId,
            eventType.ToString(),
            timestampUtc,
            actorKind.ToString(),
            actorId,
            errorCode,
            correlationId,
            previousState,
            newState);
    }

    private static bool IsAsciiLetterOrDigit(char value) =>
        value is >= 'A' and <= 'Z' or
            >= 'a' and <= 'z' or
            >= '0' and <= '9';

    private static bool IsSafeIdentifier(
        string? value,
        int maxLength,
        bool allowAt)
    {
        if (string.IsNullOrEmpty(value) ||
            value.Length > maxLength)
            return false;

        return value.All(c =>
            IsAsciiLetterOrDigit(c) ||
            c is '-' or '_' or '.' or ':' ||
            (allowAt && c == '@'));
    }

    private static bool IsSafeErrorCode(string value)
    {
        if (value.Length == 0 || value.Length > 128)
            return false;

        return value.All(c =>
            c is >= 'A' and <= 'Z' or
                >= '0' and <= '9' or '_');
    }
}