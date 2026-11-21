using Wayhunter.Application.Modules.HomeAdsAnalytics.Interfaces;
using Wayhunter.Application.Modules.HomeAdsAnalytics.Services;
using Wayhunter.Infrastructure.Persistence.Models.HomeAdsAnalytics;
using Wayhunter.Infrastructure.Persistence.Repositories.HomeAdsAnalytics;
using Xunit;

namespace Wayhunter.UnitTests.Modules.HomeAdsAnalytics;

public sealed class AdvertisementImageUploadAuditEventRowMapperTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);

    private static AdvertisementImageAuditEvent CreateEvent() =>
        AdvertisementImageAuditEventFactory.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            AdvertisementImageAuditEventType.OperationPrepared,
            Now,
            AdvertisementImageAuditActorKind.Administrator,
            "trusted-admin");

    private static AdvertisementImageUploadAuditEventRow CreateRow() =>
        AdvertisementImageUploadAuditEventRowMapper.ToRow(CreateEvent());

    [Fact]
    public void ValidEvent_MapsToSqlRow()
    {
        var item = CreateEvent();

        var row =
            AdvertisementImageUploadAuditEventRowMapper.ToRow(item);

        Assert.Equal(item.EventId, row.EventId);
        Assert.Equal(item.UploadOperationId, row.UploadOperationId);
        Assert.Equal("OperationPrepared", row.EventType);
        Assert.Equal("Administrator", row.ActorKind);
        Assert.Equal("trusted-admin", row.ActorId);
        Assert.Equal(Now, row.TimestampUtc);
    }

    [Fact]
    public void RoundTrip_PreservesAllFields()
    {
        var item = AdvertisementImageAuditEventFactory.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            AdvertisementImageAuditEventType.ProviderUploadOutcomeUnknown,
            Now,
            AdvertisementImageAuditActorKind.RecoveryWorker,
            "worker-01",
            errorCode: "PROVIDER_TIMEOUT",
            correlationId: "request-123",
            previousState: AdvertisementImageRecoveryState.PendingUpload,
            newState: AdvertisementImageRecoveryState.UploadOutcomeUnknown);

        var row =
            AdvertisementImageUploadAuditEventRowMapper.ToRow(item);

        var restored =
            AdvertisementImageUploadAuditEventRowMapper.ToEvent(row);

        Assert.Equal(item.EventId, restored.EventId);
        Assert.Equal(item.UploadOperationId, restored.UploadOperationId);
        Assert.Equal(item.EventType, restored.EventType);
        Assert.Equal(item.TimestampUtc, restored.TimestampUtc);
        Assert.Equal(item.ActorKind, restored.ActorKind);
        Assert.Equal(item.ActorId, restored.ActorId);
        Assert.Equal(item.ErrorCode, restored.ErrorCode);
        Assert.Equal(item.CorrelationId, restored.CorrelationId);
        Assert.Equal(item.PreviousState, restored.PreviousState);
        Assert.Equal(item.NewState, restored.NewState);
    }

    [Fact]
    public void RoundTrip_AllowsAbsentOptionalFields()
    {
        var row = CreateRow();

        var restored =
            AdvertisementImageUploadAuditEventRowMapper.ToEvent(row);

        Assert.Null(restored.ErrorCode);
        Assert.Null(restored.CorrelationId);
        Assert.Null(restored.PreviousState);
        Assert.Null(restored.NewState);
    }

    [Fact]
    public void UnknownEventType_IsRejected()
    {
        var row = CreateRow();
        row.EventType = "UnauthorizedEvent";

        Assert.Throws<InvalidOperationException>(() =>
            AdvertisementImageUploadAuditEventRowMapper.ToEvent(row));
    }

    [Fact]
    public void NumericEventTypeString_IsRejected()
    {
        var row = CreateRow();
        row.EventType = "0";

        Assert.Throws<InvalidOperationException>(() =>
            AdvertisementImageUploadAuditEventRowMapper.ToEvent(row));
    }

    [Fact]
    public void UnknownActorKind_IsRejected()
    {
        var row = CreateRow();
        row.ActorKind = "UnknownActor";

        Assert.Throws<InvalidOperationException>(() =>
            AdvertisementImageUploadAuditEventRowMapper.ToEvent(row));
    }

    [Fact]
    public void InvalidStoredRecoveryState_IsRejected()
    {
        var row = CreateRow();
        row.NewState = 999;

        Assert.Throws<InvalidOperationException>(() =>
            AdvertisementImageUploadAuditEventRowMapper.ToEvent(row));
    }

    [Fact]
    public void UnsafeStoredMetadata_IsRejected()
    {
        var row = CreateRow();
        row.ErrorCode = "HTTP: secret-token";

        Assert.Throws<InvalidOperationException>(() =>
            AdvertisementImageUploadAuditEventRowMapper.ToEvent(row));
    }

    [Fact]
    public void NonUtcStoredTimestamp_IsRejected()
    {
        var row = CreateRow();
        row.TimestampUtc = Now.ToOffset(TimeSpan.FromHours(5.5));

        Assert.Throws<InvalidOperationException>(() =>
            AdvertisementImageUploadAuditEventRowMapper.ToEvent(row));
    }
}