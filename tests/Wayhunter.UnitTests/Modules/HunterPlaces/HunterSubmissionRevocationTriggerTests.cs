
using System;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.HunterPlaces.Interfaces;
using Wayhunter.Application.Modules.HunterPlaces.Services;
using Wayhunter.Domain.Modules.HunterPlaces.Enums;
using Wayhunter.Infrastructure.Persistence.Repositories.HunterPlaces;
using Xunit;

namespace Wayhunter.UnitTests.Modules.HunterPlaces;

public sealed class HunterSubmissionRevocationTriggerTests
{
    private static readonly DateTimeOffset IssuedAt =
        new(2026, 11, 15, 10, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset ExpiresAt =
        IssuedAt.AddMinutes(10);

    private static readonly DateTimeOffset ReceivedAt =
        IssuedAt.AddMinutes(5);

    private sealed class FailingRevocationStore
        : IHunterCaptureSessionRevocationStore
    {
        public Task<int> RevokeBySubmissionIdAsync(
            Guid hunterSubmissionId,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException(
                "Simulated revocation storage failure.");
        }

        public Task<int> RevokeByHunterIdAsync(
            Guid hunterId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(0);
        }
    }

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

    [Theory]
    [InlineData(HunterSubmissionStatus.PendingReview)]
    [InlineData(HunterSubmissionStatus.Held)]
    [InlineData(HunterSubmissionStatus.Approved)]
    [InlineData(HunterSubmissionStatus.Rejected)]
    public async Task NonDraftStatus_ShouldRevokeSession(
        HunterSubmissionStatus newStatus)
    {
        var store = new InMemoryHunterCaptureSessionStore();

        var hunterId = Guid.NewGuid();
        var submissionId = Guid.NewGuid();

        var sessionId = await CreateSessionAsync(
            store,
            hunterId,
            submissionId);

        var service =
            new HunterSubmissionRevocationTriggerService(store);

        var revoked = await service.HandleStatusChangedAsync(
            submissionId,
            newStatus);

        Assert.Equal(1, revoked);

        Assert.False(await ConsumeAsync(
            store,
            sessionId,
            hunterId,
            submissionId));
    }

    [Fact]
    public async Task DraftStatus_ShouldNotRevokeSession()
    {
        var store = new InMemoryHunterCaptureSessionStore();

        var hunterId = Guid.NewGuid();
        var submissionId = Guid.NewGuid();

        var sessionId = await CreateSessionAsync(
            store,
            hunterId,
            submissionId);

        var service =
            new HunterSubmissionRevocationTriggerService(store);

        var revoked = await service.HandleStatusChangedAsync(
            submissionId,
            HunterSubmissionStatus.Draft);

        Assert.Equal(0, revoked);

        Assert.True(await ConsumeAsync(
            store,
            sessionId,
            hunterId,
            submissionId));
    }

    [Fact]
    public async Task OtherSubmission_ShouldRemainUsable()
    {
        var store = new InMemoryHunterCaptureSessionStore();

        var hunterId = Guid.NewGuid();

        var oldSubmissionId = Guid.NewGuid();
        var newDraftId = Guid.NewGuid();

        var oldSession = await CreateSessionAsync(
            store,
            hunterId,
            oldSubmissionId);

        var newSession = await CreateSessionAsync(
            store,
            hunterId,
            newDraftId);

        var service =
            new HunterSubmissionRevocationTriggerService(store);

        var revoked = await service.HandleStatusChangedAsync(
            oldSubmissionId,
            HunterSubmissionStatus.Rejected);

        Assert.Equal(1, revoked);

        Assert.False(await ConsumeAsync(
            store,
            oldSession,
            hunterId,
            oldSubmissionId));

        Assert.True(await ConsumeAsync(
            store,
            newSession,
            hunterId,
            newDraftId));
    }

    [Fact]
    public async Task RepeatedNotification_ShouldBeIdempotent()
    {
        var store = new InMemoryHunterCaptureSessionStore();

        var hunterId = Guid.NewGuid();
        var submissionId = Guid.NewGuid();

        var sessionId = await CreateSessionAsync(
            store,
            hunterId,
            submissionId);

        var service =
            new HunterSubmissionRevocationTriggerService(store);

        var first = await service.HandleStatusChangedAsync(
            submissionId,
            HunterSubmissionStatus.PendingReview);

        var second = await service.HandleStatusChangedAsync(
            submissionId,
            HunterSubmissionStatus.PendingReview);

        Assert.Equal(1, first);
        Assert.Equal(0, second);

        Assert.False(await ConsumeAsync(
            store,
            sessionId,
            hunterId,
            submissionId));
    }

    [Fact]
    public async Task AlreadyConsumedSession_ShouldNotCountAsRevoked()
    {
        var store = new InMemoryHunterCaptureSessionStore();

        var hunterId = Guid.NewGuid();
        var submissionId = Guid.NewGuid();

        var sessionId = await CreateSessionAsync(
            store,
            hunterId,
            submissionId);

        Assert.True(await ConsumeAsync(
            store,
            sessionId,
            hunterId,
            submissionId));

        var service =
            new HunterSubmissionRevocationTriggerService(store);

        var revoked = await service.HandleStatusChangedAsync(
            submissionId,
            HunterSubmissionStatus.Approved);

        Assert.Equal(0, revoked);

        Assert.False(await ConsumeAsync(
            store,
            sessionId,
            hunterId,
            submissionId));
    }

    [Fact]
    public async Task EmptySubmissionId_ShouldThrow()
    {
        var store = new InMemoryHunterCaptureSessionStore();

        var service =
            new HunterSubmissionRevocationTriggerService(store);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.HandleStatusChangedAsync(
                Guid.Empty,
                HunterSubmissionStatus.PendingReview));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(999)]
    public async Task InvalidStatus_ShouldThrow(int statusValue)
    {
        var store = new InMemoryHunterCaptureSessionStore();

        var service =
            new HunterSubmissionRevocationTriggerService(store);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => service.HandleStatusChangedAsync(
                Guid.NewGuid(),
                (HunterSubmissionStatus)statusValue));
    }

    [Fact]
    public async Task CancelledRequest_ShouldNotRevokeSession()
    {
        var store = new InMemoryHunterCaptureSessionStore();

        var hunterId = Guid.NewGuid();
        var submissionId = Guid.NewGuid();

        var sessionId = await CreateSessionAsync(
            store,
            hunterId,
            submissionId);

        var service =
            new HunterSubmissionRevocationTriggerService(store);

        using var cts = new CancellationTokenSource();

        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.HandleStatusChangedAsync(
                submissionId,
                HunterSubmissionStatus.Held,
                cts.Token));

        Assert.True(await ConsumeAsync(
            store,
            sessionId,
            hunterId,
            submissionId));
    }

    [Fact]
    public async Task RevocationFailure_ShouldPropagate()
    {
        var store = new FailingRevocationStore();

        var service =
            new HunterSubmissionRevocationTriggerService(store);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.HandleStatusChangedAsync(
                Guid.NewGuid(),
                HunterSubmissionStatus.Rejected));
    }

    [Fact]
    public void NullStore_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            () => new HunterSubmissionRevocationTriggerService(
                null!));
    }
}
