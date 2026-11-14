
using System;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.HunterPlaces.Interfaces;
using Wayhunter.Application.Modules.HunterPlaces.Services;
using Xunit;

namespace Wayhunter.UnitTests.Modules.HunterPlaces;

public sealed class HunterCaptureSessionServiceTests
{
    private static readonly Guid HunterId = Guid.NewGuid();
    private static readonly Guid SubmissionId = Guid.NewGuid();
    private static readonly Guid SessionId = Guid.NewGuid();

    private static readonly DateTimeOffset FixedTime =
        new(2026, 11, 14, 10, 0, 0, TimeSpan.Zero);

    private sealed class FakeTimeProvider : TimeProvider
    {
        public DateTimeOffset CurrentTime { get; set; } =
            FixedTime;

        public override DateTimeOffset GetUtcNow()
        {
            return CurrentTime;
        }
    }

    private sealed class FakeCaptureSessionStore
        : IHunterCaptureSessionStore
    {
        public int CreateCount { get; private set; }
        public int ConsumeCount { get; private set; }

        public Guid LastHunterId { get; private set; }
        public Guid LastSubmissionId { get; private set; }
        public Guid LastSessionId { get; private set; }

        public DateTimeOffset LastIssuedAtUtc { get; private set; }
        public DateTimeOffset LastExpiresAtUtc { get; private set; }
        public DateTimeOffset LastReceivedAtUtc { get; private set; }

        public CancellationToken LastToken { get; private set; }

        public bool ConsumeResult { get; set; } = true;

        public Task<Guid> CreateAsync(
            Guid hunterId,
            Guid hunterSubmissionId,
            DateTimeOffset issuedAtUtc,
            DateTimeOffset expiresAtUtc,
            CancellationToken cancellationToken = default)
        {
            CreateCount++;
            LastHunterId = hunterId;
            LastSubmissionId = hunterSubmissionId;
            LastIssuedAtUtc = issuedAtUtc;
            LastExpiresAtUtc = expiresAtUtc;
            LastToken = cancellationToken;

            return Task.FromResult(SessionId);
        }

        public Task<bool> TryConsumeAsync(
            Guid captureSessionId,
            Guid authenticatedHunterId,
            Guid hunterSubmissionId,
            DateTimeOffset serverReceivedAtUtc,
            CancellationToken cancellationToken = default)
        {
            ConsumeCount++;
            LastSessionId = captureSessionId;
            LastHunterId = authenticatedHunterId;
            LastSubmissionId = hunterSubmissionId;
            LastReceivedAtUtc = serverReceivedAtUtc;
            LastToken = cancellationToken;

            return Task.FromResult(ConsumeResult);
        }
    }

    private static (
        HunterCaptureSessionService Service,
        FakeCaptureSessionStore Store,
        FakeTimeProvider Clock)
        CreateService()
    {
        var store = new FakeCaptureSessionStore();
        var clock = new FakeTimeProvider();

        var service = new HunterCaptureSessionService(
            store, clock);

        return (service, store, clock);
    }

    [Fact]
    public void NullStore_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new HunterCaptureSessionService(
                null!, new FakeTimeProvider()));
    }

    [Fact]
    public void NullTimeProvider_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new HunterCaptureSessionService(
                new FakeCaptureSessionStore(), null!));
    }

    [Fact]
    public async Task Start_ShouldCreateSession()
    {
        var (service, store, _) = CreateService();

        var result = await service.StartAsync(
            HunterId, SubmissionId);

        Assert.Equal(SessionId, result);
        Assert.Equal(1, store.CreateCount);
        Assert.Equal(HunterId, store.LastHunterId);
        Assert.Equal(SubmissionId, store.LastSubmissionId);
    }

    [Fact]
    public async Task Start_ShouldUseServerTime()
    {
        var (service, store, clock) = CreateService();

        clock.CurrentTime = FixedTime.AddHours(2);

        await service.StartAsync(HunterId, SubmissionId);

        Assert.Equal(
            clock.CurrentTime,
            store.LastIssuedAtUtc);
    }

    [Fact]
    public async Task Start_ShouldSetTenMinuteExpiry()
    {
        var (service, store, _) = CreateService();

        await service.StartAsync(HunterId, SubmissionId);

        Assert.Equal(
            TimeSpan.FromMinutes(10),
            store.LastExpiresAtUtc - store.LastIssuedAtUtc);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task Start_WithEmptyId_ShouldThrow(int field)
    {
        var (service, store, _) = CreateService();

        var hunterId = field == 0
            ? Guid.Empty : HunterId;

        var submissionId = field == 1
            ? Guid.Empty : SubmissionId;

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.StartAsync(hunterId, submissionId));

        Assert.Equal(0, store.CreateCount);
    }

    [Fact]
    public async Task Start_WithCancelledToken_ShouldThrow()
    {
        var (service, store, _) = CreateService();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.StartAsync(
                HunterId, SubmissionId, cts.Token));

        Assert.Equal(0, store.CreateCount);
    }

    [Fact]
    public async Task Start_ShouldForwardCancellationToken()
    {
        var (service, store, _) = CreateService();

        using var cts = new CancellationTokenSource();

        await service.StartAsync(
            HunterId, SubmissionId, cts.Token);

        Assert.Equal(cts.Token, store.LastToken);
    }

    [Fact]
    public async Task Consume_ShouldForwardSessionDetails()
    {
        var (service, store, _) = CreateService();

        var result = await service.TryConsumeAsync(
            SessionId, HunterId, SubmissionId);

        Assert.True(result);
        Assert.Equal(1, store.ConsumeCount);
        Assert.Equal(SessionId, store.LastSessionId);
        Assert.Equal(HunterId, store.LastHunterId);
        Assert.Equal(SubmissionId, store.LastSubmissionId);
    }

    [Fact]
    public async Task Consume_ShouldUseServerTime()
    {
        var (service, store, clock) = CreateService();

        clock.CurrentTime = FixedTime.AddMinutes(4);

        await service.TryConsumeAsync(
            SessionId, HunterId, SubmissionId);

        Assert.Equal(
            clock.CurrentTime,
            store.LastReceivedAtUtc);
    }

    [Fact]
    public async Task Consume_WhenStoreRejects_ShouldReturnFalse()
    {
        var (service, store, _) = CreateService();

        store.ConsumeResult = false;

        var result = await service.TryConsumeAsync(
            SessionId, HunterId, SubmissionId);

        Assert.False(result);
        Assert.Equal(1, store.ConsumeCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Consume_WithEmptyId_ShouldReturnFalse(
        int field)
    {
        var (service, store, _) = CreateService();

        var sessionId = field == 0
            ? Guid.Empty : SessionId;

        var hunterId = field == 1
            ? Guid.Empty : HunterId;

        var submissionId = field == 2
            ? Guid.Empty : SubmissionId;

        var result = await service.TryConsumeAsync(
            sessionId, hunterId, submissionId);

        Assert.False(result);
        Assert.Equal(0, store.ConsumeCount);
    }

    [Fact]
    public async Task Consume_WithCancelledToken_ShouldThrow()
    {
        var (service, store, _) = CreateService();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.TryConsumeAsync(
                SessionId,
                HunterId,
                SubmissionId,
                cts.Token));

        Assert.Equal(0, store.ConsumeCount);
    }

    [Fact]
    public async Task Consume_ShouldForwardCancellationToken()
    {
        var (service, store, _) = CreateService();

        using var cts = new CancellationTokenSource();

        await service.TryConsumeAsync(
            SessionId, HunterId, SubmissionId, cts.Token);

        Assert.Equal(cts.Token, store.LastToken);
    }
}
