using Wayhunter.Application.Modules.HomeAdsAnalytics.Interfaces;
using Wayhunter.Application.Modules.HomeAdsAnalytics.Services;
using Wayhunter.Infrastructure.Persistence.Models.HomeAdsAnalytics;

namespace Wayhunter.Infrastructure.Persistence.Repositories.HomeAdsAnalytics;

/// <summary>
/// Offline mapper between validated audit events and provisional SQL rows.
/// No database writes, deletes or runtime registration.
/// </summary>
public static class AdvertisementImageUploadAuditEventRowMapper
{
    public static AdvertisementImageUploadAuditEventRow ToRow(
        AdvertisementImageAuditEvent auditEvent)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);

        return new AdvertisementImageUploadAuditEventRow
        {
            EventId = auditEvent.EventId,
            UploadOperationId = auditEvent.UploadOperationId,
            EventType = auditEvent.EventType,
            TimestampUtc = auditEvent.TimestampUtc,
            ActorKind = auditEvent.ActorKind,
            ActorId = auditEvent.ActorId,
            ErrorCode = auditEvent.ErrorCode,
            CorrelationId = auditEvent.CorrelationId,
            PreviousState = auditEvent.PreviousState is { } previous
                ? (int)previous
                : null,
            NewState = auditEvent.NewState is { } next
                ? (int)next
                : null
        };
    }

    public static AdvertisementImageAuditEvent ToEvent(
        AdvertisementImageUploadAuditEventRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        var eventType = ParseExactEnum<AdvertisementImageAuditEventType>(
            row.EventType);

        var actorKind = ParseExactEnum<AdvertisementImageAuditActorKind>(
            row.ActorKind);

        var previousState = ParseState(row.PreviousState);
        var newState = ParseState(row.NewState);

        try
        {
            return AdvertisementImageAuditEventFactory.Create(
                row.EventId,
                row.UploadOperationId,
                eventType,
                row.TimestampUtc,
                actorKind,
                row.ActorId,
                row.ErrorCode,
                row.CorrelationId,
                previousState,
                newState);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidOperationException(
                "Stored audit event failed validation.",
                exception);
        }
    }

    private static TEnum ParseExactEnum<TEnum>(string? value)
        where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value) ||
            !Enum.TryParse<TEnum>(
                value,
                ignoreCase: false,
                out var parsed) ||
            !Enum.IsDefined(parsed) ||
            !string.Equals(
                parsed.ToString(),
                value,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Stored audit enum value is invalid.");
        }

        return parsed;
    }

    private static AdvertisementImageRecoveryState? ParseState(
        int? value)
    {
        if (value is null)
            return null;

        var state = (AdvertisementImageRecoveryState)value.Value;

        if (!Enum.IsDefined(state))
        {
            throw new InvalidOperationException(
                "Stored audit recovery state is invalid.");
        }

        return state;
    }
}