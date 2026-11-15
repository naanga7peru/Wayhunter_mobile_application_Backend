
using System;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Infrastructure.Persistence.Repositories.HunterPlaces;
using Xunit;

namespace Wayhunter.UnitTests.Modules.HunterPlaces;

public sealed class HunterCaptureSessionRevocationTests
{
    private static readonly DateTimeOffset IssuedAt =
        new(2026, 11, 15, 10, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset ExpiresAt =
        IssuedAt.AddMinutes(10);

    private static readonly DateTimeOffset ReceivedAt =
        IssuedAt.AddMinutes(5);

    private static Task<Guid> CreateSessionAsync(
        InMemoryHunterCaptureSessionStore store,
        Guid hunterId,
        Guid submissionId)
    {
        return store.CreateAsync(
            hunterId,
            submissionId,
            IssuedAt,
            ExpiresAt);
    }

    private static Task<bool> ConsumeAsync(
        InMemoryHunterCaptureSessionStore store,
        Guid sessionId,
        Guid hunterId,
        Guid submissionId)
    {
        return store.TryConsumeAsync(
            sessionId,
            hunterId,
            submissionId,
            ReceivedAt);
    }

    [Fact]
    public async Task RevokedSubmission_ShouldRejectConsumption()
    {
        var store = new InMemoryHunterCaptureSessionStore();

        var hunterId = Guid.NewGuid();
        var submissionId = Guid.NewGuid();

        var sessionId = await CreateSessionAsync(
            store, hunterId, submissionId);

        var count = await store.RevokeBySubmissionIdAsync(
            submissionId);

        Assert.Equal(1, count);

        Assert.False(await ConsumeAsync(
            store, sessionId, hunterId, submissionId));
    }

    [Fact]
    public async Task SubmissionRevocation_ShouldRevokeAllItsSessions()
    {
        var store = new InMemoryHunterCaptureSessionStore();

        var hunterId = Guid.NewGuid();
        var submissionId = Guid.NewGuid();

        var first = await CreateSessionAsync(
            store, hunterId, submissionId);

        var second = await CreateSessionAsync(
            store, hunterId, submissionId);

        var count = await store.RevokeBySubmissionIdAsync(
            submissionId);

        Assert.Equal(2, count);

        Assert.False(await ConsumeAsync(
            store, first, hunterId, submissionId));

        Assert.False(await ConsumeAsync(
            store, second, hunterId, submissionId));
    }

    [Fact]
    public async Task RevokingSubmission_ShouldNotBlockNewDifferentDraft()
    {
        var store = new InMemoryHunterCaptureSessionStore();

        var hunterId = Guid.NewGuid();
        var oldSubmissionId = Guid.NewGuid();
        var newSubmissionId = Guid.NewGuid();

        var oldSession = await CreateSessionAsync(
            store, hunterId, oldSubmissionId);

        var newSession = await CreateSessionAsync(
            store, hunterId, newSubmissionId);

        Assert.Equal(
            1,
            await store.RevokeBySubmissionIdAsync(
                oldSubmissionId));

        Assert.False(await ConsumeAsync(
            store, oldSession, hunterId, oldSubmissionId));

        Assert.True(await ConsumeAsync(
            store, newSession, hunterId, newSubmissionId));
    }

    [Fact]
    public async Task HunterRevocation_ShouldInvalidateAllHisSessions()
    {
        var store = new InMemoryHunterCaptureSessionStore();

        var hunterId = Guid.NewGuid();
        var firstSubmission = Guid.NewGuid();
        var secondSubmission = Guid.NewGuid();

        var first = await CreateSessionAsync(
            store, hunterId, firstSubmission);

        var second = await CreateSessionAsync(
            store, hunterId, secondSubmission);

        var count = await store.RevokeByHunterIdAsync(
            hunterId);

        Assert.Equal(2, count);

        Assert.False(await ConsumeAsync(
            store, first, hunterId, firstSubmission));

        Assert.False(await ConsumeAsync(
            store, second, hunterId, secondSubmission));
    }

    [Fact]
    public async Task HunterRevocation_ShouldNotAffectOtherHunter()
    {
        var store = new InMemoryHunterCaptureSessionStore();

        var firstHunter = Guid.NewGuid();
        var secondHunter = Guid.NewGuid();

        var firstSubmission = Guid.NewGuid();
        var secondSubmission = Guid.NewGuid();

        var first = await CreateSessionAsync(
            store, firstHunter, firstSubmission);

        var second = await CreateSessionAsync(
            store, secondHunter, secondSubmission);

        Assert.Equal(
            1,
            await store.RevokeByHunterIdAsync(firstHunter));

        Assert.False(await ConsumeAsync(
            store, first, firstHunter, firstSubmission));

        Assert.True(await ConsumeAsync(
            store, second, secondHunter, secondSubmission));
    }

    [Fact]
    public async Task RepeatedRevocation_ShouldBeIdempotent()
    {
        var store = new InMemoryHunterCaptureSessionStore();

        var hunterId = Guid.NewGuid();
        var submissionId = Guid.NewGuid();

        var sessionId = await CreateSessionAsync(
            store, hunterId, submissionId);

        Assert.Equal(
            1,
            await store.RevokeBySubmissionIdAsync(
                submissionId));

        Assert.Equal(
            0,
            await store.RevokeBySubmissionIdAsync(
                submissionId));

        Assert.Equal(
            0,
            await store.RevokeByHunterIdAsync(hunterId));

        Assert.False(await ConsumeAsync(
            store, sessionId, hunterId, submissionId));
    }

    [Fact]
    public async Task ConsumedSession_ShouldNotBeCountedAgain()
    {
        var store = new InMemoryHunterCaptureSessionStore();

        var hunterId = Guid.NewGuid();
        var submissionId = Guid.NewGuid();

        var sessionId = await CreateSessionAsync(
            store, hunterId, submissionId);

        Assert.True(await ConsumeAsync(
            store, sessionId, hunterId, submissionId));

        Assert.Equal(
            0,
            await store.RevokeBySubmissionIdAsync(
                submissionId));

        Assert.False(await ConsumeAsync(
            store, sessionId, hunterId, submissionId));
    }

    [Fact]
    public async Task UnknownSubmission_ShouldRevokeNothing()
    {
        var store = new InMemoryHunterCaptureSessionStore();

        Assert.Equal(
            0,
            await store.RevokeBySubmissionIdAsync(
                Guid.NewGuid()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EmptyId_ShouldBeRejected(bool byHunter)
    {
        var store = new InMemoryHunterCaptureSessionStore();

        if (byHunter)
        {
            await Assert.ThrowsAsync<ArgumentException>(
                () => store.RevokeByHunterIdAsync(
                    Guid.Empty));
        }
        else
        {
            await Assert.ThrowsAsync<ArgumentException>(
                () => store.RevokeBySubmissionIdAsync(
                    Guid.Empty));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancelledRevocation_ShouldNotChangeSession(
        bool byHunter)
    {
        var store = new InMemoryHunterCaptureSessionStore();

        var hunterId = Guid.NewGuid();
        var submissionId = Guid.NewGuid();

        var sessionId = await CreateSessionAsync(
            store, hunterId, submissionId);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        if (byHunter)
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => store.RevokeByHunterIdAsync(
                    hunterId, cts.Token));
        }
        else
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => store.RevokeBySubmissionIdAsync(
                    submissionId, cts.Token));
        }

        Assert.True(await ConsumeAsync(
            store, sessionId, hunterId, submissionId));
    }

    [Fact]
    public async Task ConcurrentRevokeAndConsume_ShouldBeAtomic()
    {
        var store = new InMemoryHunterCaptureSessionStore();

        var hunterId = Guid.NewGuid();
        var submissionId = Guid.NewGuid();

        var sessionId = await CreateSessionAsync(
            store, hunterId, submissionId);

        var revokeTask = Task.Run(
            async () => await store.RevokeBySubmissionIdAsync(
                submissionId));

        var consumeTask = Task.Run(
            async () => await ConsumeAsync(
                store, sessionId, hunterId, submissionId));

        await Task.WhenAll(revokeTask, consumeTask);

        var revokedCount = await revokeTask;
        var consumed = await consumeTask;

        Assert.True(
            (revokedCount == 1 && !consumed) ||
            (revokedCount == 0 && consumed));
        // The same session is unusable afterwards.
        Assert.False(await ConsumeAsync(
            store, sessionId, hunterId, submissionId));
    }
}
