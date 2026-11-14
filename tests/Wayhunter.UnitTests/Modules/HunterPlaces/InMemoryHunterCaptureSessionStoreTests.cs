
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Infrastructure.Persistence.Repositories.HunterPlaces;
using Xunit;

namespace Wayhunter.UnitTests.Modules.HunterPlaces;

public sealed class InMemoryHunterCaptureSessionStoreTests
{
    private static readonly Guid HunterId = Guid.NewGuid();
    private static readonly Guid SubmissionId = Guid.NewGuid();

    private static readonly DateTimeOffset IssuedAt =
        new(2026, 11, 14, 8, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset ExpiresAt =
        IssuedAt.AddMinutes(10);

    private static readonly DateTimeOffset ReceivedAt =
        IssuedAt.AddMinutes(5);

    private static Task<Guid> CreateValidSessionAsync(
        InMemoryHunterCaptureSessionStore store)
    {
        return store.CreateAsync(
            HunterId,
            SubmissionId,
            IssuedAt,
            ExpiresAt);
    }

    private static Task<bool> ConsumeAsync(
        InMemoryHunterCaptureSessionStore store,
        Guid sessionId,
        Guid? hunterId = null,
        Guid? submissionId = null,
        DateTimeOffset? receivedAt = null)
    {
        return store.TryConsumeAsync(
            sessionId,
            hunterId ?? HunterId,
            submissionId ?? SubmissionId,
            receivedAt ?? ReceivedAt);
    }

    [Fact]
    public async Task Create_ShouldReturnNonEmptySessionId()
    {
        var store = new InMemoryHunterCaptureSessionStore();

        var id = await CreateValidSessionAsync(store);

        Assert.NotEqual(Guid.Empty, id);
    }

    [Fact]
    public async Task Create_ShouldGenerateUniqueSessionIds()
    {
        var store = new InMemoryHunterCaptureSessionStore();

        var first = await CreateValidSessionAsync(store);
        var second = await CreateValidSessionAsync(store);

        Assert.NotEqual(first, second);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task Create_WithEmptyId_ShouldThrow(int field)
    {
        var store = new InMemoryHunterCaptureSessionStore();

        var hunterId = field == 0 ? Guid.Empty : HunterId;
        var submissionId =
            field == 1 ? Guid.Empty : SubmissionId;

        await Assert.ThrowsAsync<ArgumentException>(() =>
            store.CreateAsync(
                hunterId, submissionId, IssuedAt, ExpiresAt));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public async Task Create_WithInvalidTimeRange_ShouldThrow(
        int scenario)
    {
        var store = new InMemoryHunterCaptureSessionStore();

        var issued = IssuedAt;
        var expires = ExpiresAt;

        switch (scenario)
        {
            case 0:
                issued = default;
                break;

            case 1:
                expires = default;
                break;

            case 2:
                expires = issued;
                break;

            case 3:
                expires = issued.AddSeconds(-1);
                break;

            case 4:
                issued = issued.ToOffset(
                    TimeSpan.FromHours(5.5));
                break;

            case 5:
                expires = expires.ToOffset(
                    TimeSpan.FromHours(5.5));
                break;
        }

        await Assert.ThrowsAsync<ArgumentException>(() =>
            store.CreateAsync(
                HunterId, SubmissionId, issued, expires));
    }

    [Fact]
    public async Task FirstConsume_ShouldSucceed_AndReplayFail()
    {
        var store = new InMemoryHunterCaptureSessionStore();
        var id = await CreateValidSessionAsync(store);

        var first = await ConsumeAsync(store, id);
        var second = await ConsumeAsync(store, id);

        Assert.True(first);
        Assert.False(second);
    }

    [Fact]
    public async Task WrongHunter_ShouldNotConsumeSession()
    {
        var store = new InMemoryHunterCaptureSessionStore();
        var id = await CreateValidSessionAsync(store);

        var wrong = await ConsumeAsync(
            store, id, hunterId: Guid.NewGuid());

        var correct = await ConsumeAsync(store, id);

        Assert.False(wrong);
        Assert.True(correct);
    }

    [Fact]
    public async Task WrongSubmission_ShouldNotConsumeSession()
    {
        var store = new InMemoryHunterCaptureSessionStore();
        var id = await CreateValidSessionAsync(store);

        var wrong = await ConsumeAsync(
            store, id, submissionId: Guid.NewGuid());

        var correct = await ConsumeAsync(store, id);

        Assert.False(wrong);
        Assert.True(correct);
    }

    [Fact]
    public async Task UnknownSession_ShouldReturnFalse()
    {
        var store = new InMemoryHunterCaptureSessionStore();

        var result = await ConsumeAsync(
            store, Guid.NewGuid());

        Assert.False(result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task InvalidConsumeInput_ShouldReturnFalse(
        int scenario)
    {
        var store = new InMemoryHunterCaptureSessionStore();
        var id = await CreateValidSessionAsync(store);

        var sessionId = id;
        var hunterId = HunterId;
        var submissionId = SubmissionId;
        var received = ReceivedAt;

        switch (scenario)
        {
            case 0:
                sessionId = Guid.Empty;
                break;

            case 1:
                hunterId = Guid.Empty;
                break;

            case 2:
                submissionId = Guid.Empty;
                break;

            case 3:
                received = default;
                break;
        }

        var result = await store.TryConsumeAsync(
            sessionId, hunterId, submissionId, received);

        Assert.False(result);

        // Failed attempts must not consume a valid session.
        Assert.True(await ConsumeAsync(store, id));
    }

    [Fact]
    public async Task BeforeIssueTime_ShouldReturnFalse()
    {
        var store = new InMemoryHunterCaptureSessionStore();
        var id = await CreateValidSessionAsync(store);

        var result = await ConsumeAsync(
            store, id, receivedAt: IssuedAt.AddTicks(-1));

        Assert.False(result);
    }

    [Fact]
    public async Task ExactlyAtIssueTime_ShouldSucceed()
    {
        var store = new InMemoryHunterCaptureSessionStore();
        var id = await CreateValidSessionAsync(store);

        var result = await ConsumeAsync(
            store, id, receivedAt: IssuedAt);

        Assert.True(result);
    }

    [Fact]
    public async Task JustBeforeExpiry_ShouldSucceed()
    {
        var store = new InMemoryHunterCaptureSessionStore();
        var id = await CreateValidSessionAsync(store);

        var result = await ConsumeAsync(
            store,
            id,
            receivedAt: ExpiresAt.AddTicks(-1));

        Assert.True(result);
    }

    [Fact]
    public async Task ExactlyAtExpiry_ShouldReturnFalse()
    {
        var store = new InMemoryHunterCaptureSessionStore();
        var id = await CreateValidSessionAsync(store);

        var result = await ConsumeAsync(
            store, id, receivedAt: ExpiresAt);

        Assert.False(result);
    }

    [Fact]
    public async Task AfterExpiry_ShouldReturnFalse()
    {
        var store = new InMemoryHunterCaptureSessionStore();
        var id = await CreateValidSessionAsync(store);

        var result = await ConsumeAsync(
            store,
            id,
            receivedAt: ExpiresAt.AddMinutes(1));

        Assert.False(result);
    }

    [Fact]
    public async Task NonUtcReceivedTime_ShouldReturnFalse()
    {
        var store = new InMemoryHunterCaptureSessionStore();
        var id = await CreateValidSessionAsync(store);

        var nonUtc = ReceivedAt.ToOffset(
            TimeSpan.FromHours(5.5));

        var result = await ConsumeAsync(
            store, id, receivedAt: nonUtc);

        Assert.False(result);
    }

    [Fact]
    public async Task SeparateStores_ShouldNotShareSessions()
    {
        var firstStore =
            new InMemoryHunterCaptureSessionStore();

        var secondStore =
            new InMemoryHunterCaptureSessionStore();

        var id = await CreateValidSessionAsync(firstStore);

        Assert.False(await ConsumeAsync(secondStore, id));
        Assert.True(await ConsumeAsync(firstStore, id));
    }

    [Fact]
    public async Task CancelledCreate_ShouldThrow()
    {
        var store = new InMemoryHunterCaptureSessionStore();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => store.CreateAsync(
                HunterId,
                SubmissionId,
                IssuedAt,
                ExpiresAt,
                cts.Token));
    }

    [Fact]
    public async Task CancelledConsume_ShouldThrow()
    {
        var store = new InMemoryHunterCaptureSessionStore();
        var id = await CreateValidSessionAsync(store);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => store.TryConsumeAsync(
                id,
                HunterId,
                SubmissionId,
                ReceivedAt,
                cts.Token));
    }

    [Fact]
    public async Task ConcurrentConsumes_OnlyOneShouldSucceed()
    {
        var store = new InMemoryHunterCaptureSessionStore();
        var id = await CreateValidSessionAsync(store);

        var attempts = Enumerable.Range(0, 50)
            .Select(_ => Task.Run(async () =>
                await ConsumeAsync(store, id)));

        var results = await Task.WhenAll(attempts);

        Assert.Equal(1, results.Count(result => result));
        Assert.Equal(49, results.Count(result => !result));
    }
}
