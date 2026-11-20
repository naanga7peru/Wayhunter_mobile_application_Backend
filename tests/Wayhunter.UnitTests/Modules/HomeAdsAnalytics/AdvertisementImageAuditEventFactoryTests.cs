using Wayhunter.Application.Modules.HomeAdsAnalytics.Interfaces;
using Wayhunter.Application.Modules.HomeAdsAnalytics.Services;
using Xunit;

namespace Wayhunter.UnitTests.Modules.HomeAdsAnalytics;

public sealed class AdvertisementImageAuditEventFactoryTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);

    private static AdvertisementImageAuditEvent Create() =>
        AdvertisementImageAuditEventFactory.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            AdvertisementImageAuditEventType.OperationPrepared,
            Now,
            AdvertisementImageAuditActorKind.Administrator,
            "trusted-admin");

    [Fact]
    public void ValidEvent_PreservesTrustedFields()
    {
        var item = Create();

        Assert.NotEqual(Guid.Empty, item.EventId);
        Assert.NotEqual(Guid.Empty, item.UploadOperationId);
        Assert.Equal("OperationPrepared", item.EventType);
        Assert.Equal("Administrator", item.ActorKind);
        Assert.Equal("trusted-admin", item.ActorId);
        Assert.Equal(Now, item.TimestampUtc);
    }

    [Fact]
    public void ValidSanitizedMetadata_IsPreserved()
    {
        var item = AdvertisementImageAuditEventFactory.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            AdvertisementImageAuditEventType.ProviderUploadOutcomeUnknown,
            Now,
            AdvertisementImageAuditActorKind.RecoveryWorker,
            "worker-01",
            errorCode: "PROVIDER_TIMEOUT",
            correlationId: "request-123:retry",
            previousState: AdvertisementImageRecoveryState.PendingUpload,
            newState: AdvertisementImageRecoveryState.UploadOutcomeUnknown);

        Assert.Equal("PROVIDER_TIMEOUT", item.ErrorCode);
        Assert.Equal("request-123:retry", item.CorrelationId);
        Assert.Equal(
            AdvertisementImageRecoveryState.PendingUpload,
            item.PreviousState);
        Assert.Equal(
            AdvertisementImageRecoveryState.UploadOutcomeUnknown,
            item.NewState);
    }

    [Fact]
    public void EmptyIds_AreRejected()
    {
        Assert.Throws<ArgumentException>(() =>
            AdvertisementImageAuditEventFactory.Create(
                Guid.Empty, Guid.NewGuid(),
                AdvertisementImageAuditEventType.OperationPrepared,
                Now, AdvertisementImageAuditActorKind.System, "system"));

        Assert.Throws<ArgumentException>(() =>
            AdvertisementImageAuditEventFactory.Create(
                Guid.NewGuid(), Guid.Empty,
                AdvertisementImageAuditEventType.OperationPrepared,
                Now, AdvertisementImageAuditActorKind.System, "system"));
    }

    [Fact]
    public void UnknownEventType_IsRejected()
    {
        Assert.Throws<ArgumentException>(() =>
            AdvertisementImageAuditEventFactory.Create(
                Guid.NewGuid(), Guid.NewGuid(),
                (AdvertisementImageAuditEventType)999,
                Now, AdvertisementImageAuditActorKind.System, "system"));
    }

    [Fact]
    public void UnknownActorKind_IsRejected()
    {
        Assert.Throws<ArgumentException>(() =>
            AdvertisementImageAuditEventFactory.Create(
                Guid.NewGuid(), Guid.NewGuid(),
                AdvertisementImageAuditEventType.OperationPrepared,
                Now, (AdvertisementImageAuditActorKind)999, "system"));
    }

    [Fact]
    public void UnsafeActorId_IsRejected()
    {
        Assert.Throws<ArgumentException>(() =>
            AdvertisementImageAuditEventFactory.Create(
                Guid.NewGuid(), Guid.NewGuid(),
                AdvertisementImageAuditEventType.OperationPrepared,
                Now, AdvertisementImageAuditActorKind.Administrator,
                "trusted-admin" + "\nSECRET"));
    }

    [Fact]
    public void RawExceptionText_IsRejectedAsErrorCode()
    {
        Assert.Throws<ArgumentException>(() =>
            AdvertisementImageAuditEventFactory.Create(
                Guid.NewGuid(), Guid.NewGuid(),
                AdvertisementImageAuditEventType.ProviderUploadOutcomeUnknown,
                Now, AdvertisementImageAuditActorKind.System, "system",
                errorCode: "HTTP: secret-token"));
    }

    [Fact]
    public void UnsafeCorrelationId_IsRejected()
    {
        Assert.Throws<ArgumentException>(() =>
            AdvertisementImageAuditEventFactory.Create(
                Guid.NewGuid(), Guid.NewGuid(),
                AdvertisementImageAuditEventType.OperationPrepared,
                Now, AdvertisementImageAuditActorKind.System, "system",
                correlationId: "unsafe correlation"));
    }

    [Fact]
    public void NonUtcTimestamp_IsRejected()
    {
        var local = Now.ToOffset(TimeSpan.FromHours(5.5));

        Assert.Throws<ArgumentException>(() =>
            AdvertisementImageAuditEventFactory.Create(
                Guid.NewGuid(), Guid.NewGuid(),
                AdvertisementImageAuditEventType.OperationPrepared,
                local, AdvertisementImageAuditActorKind.System, "system"));
    }

    [Fact]
    public void UnknownRecoveryState_IsRejected()
    {
        Assert.Throws<ArgumentException>(() =>
            AdvertisementImageAuditEventFactory.Create(
                Guid.NewGuid(), Guid.NewGuid(),
                AdvertisementImageAuditEventType.OperationPrepared,
                Now, AdvertisementImageAuditActorKind.System, "system",
                newState: (AdvertisementImageRecoveryState)999));
    }

    [Fact]
    public void RepositoryContract_ExposesAppendOnlyMethod()
    {
        var methods =
            typeof(IAdvertisementImageAuditEventRepository).GetMethods();

        var method = Assert.Single(methods);
        Assert.Equal("TryAppendAsync", method.Name);
    }
}