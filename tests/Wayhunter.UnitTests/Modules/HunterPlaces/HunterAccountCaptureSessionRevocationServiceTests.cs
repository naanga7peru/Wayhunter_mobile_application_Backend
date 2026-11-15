
using System;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.HunterPlaces.Interfaces;
using Wayhunter.Application.Modules.HunterPlaces.Services;
using Wayhunter.Domain.Modules.IdentitySocial.Enums;
using Wayhunter.Infrastructure.Persistence.Repositories.HunterPlaces;
using Xunit;

namespace Wayhunter.UnitTests.Modules.HunterPlaces;

public sealed class HunterAccountCaptureSessionRevocationServiceTests
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
            return Task.FromResult(0);
        }

        public Task<int> RevokeByHunterIdAsync(
            Guid hunterId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromException<int>(
                new InvalidOperationException(
                    "Simulated storage failure."));
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
    [InlineData(UserAccountStatus.Suspended)]
    [InlineData(UserAccountStatus.DeletionRequested)]
    public async Task RestrictedAccount_ShouldRevokeAllSessions(
        UserAccountStatus status)
    {
        var store = new InMemoryHunterCaptureSessionStore();

        var hunterId = Guid.NewGuid();
        var firstSubmission = Guid.NewGuid();
        var secondSubmission = Guid.NewGuid();

        var first = await CreateSessionAsync(
            store, hunterId, firstSubmission);

        var second = await CreateSessionAsync(
            store, hunterId, secondSubmission);

        var service =
            new HunterAccountCaptureSessionRevocationService(store);

        var revoked = await service.HandleAccountStatusChangedAsync(
            hunterId.ToString("D"),
            status);

        Assert.Equal(2, revoked);

        Assert.False(await ConsumeAsync(
            store, first, hunterId, firstSubmission));

        Assert.False(await ConsumeAsync(
            store, second, hunterId, secondSubmission));
    }

    [Theory]
    [InlineData(UserAccountStatus.Active)]
    [InlineData(UserAccountStatus.PendingVerification)]
    public async Task NonRevokingStatus_ShouldNotRevokeSession(
        UserAccountStatus status)
    {
        var store = new InMemoryHunterCaptureSessionStore();

        var hunterId = Guid.NewGuid();
        var submissionId = Guid.NewGuid();

        var sessionId = await CreateSessionAsync(
            store, hunterId, submissionId);

        var service =
            new HunterAccountCaptureSessionRevocationService(store);

        var revoked = await service.HandleAccountStatusChangedAsync(
            hunterId.ToString("D"),
            status);

        Assert.Equal(0, revoked);

        Assert.True(await ConsumeAsync(
            store, sessionId, hunterId, submissionId));
    }

    [Fact]
    public async Task OtherHunter_ShouldRemainUnaffected()
    {
        var store = new InMemoryHunterCaptureSessionStore();

        var firstHunter = Guid.NewGuid();
        var secondHunter = Guid.NewGuid();

        var firstSubmission = Guid.NewGuid();
        var secondSubmission = Guid.NewGuid();

        var firstSession = await CreateSessionAsync(
            store, firstHunter, firstSubmission);

        var secondSession = await CreateSessionAsync(
            store, secondHunter, secondSubmission);

        var service =
            new HunterAccountCaptureSessionRevocationService(store);

        var revoked = await service.HandleAccountStatusChangedAsync(
            firstHunter.ToString("D"),
            UserAccountStatus.Suspended);

        Assert.Equal(1, revoked);

        Assert.False(await ConsumeAsync(
            store, firstSession, firstHunter, firstSubmission));

        Assert.True(await ConsumeAsync(
            store, secondSession, secondHunter, secondSubmission));
    }

    [Fact]
    public async Task RepeatedNotification_ShouldBeIdempotent()
    {
        var store = new InMemoryHunterCaptureSessionStore();

        var hunterId = Guid.NewGuid();
        var submissionId = Guid.NewGuid();

        var sessionId = await CreateSessionAsync(
            store, hunterId, submissionId);

        var service =
            new HunterAccountCaptureSessionRevocationService(store);

        var first = await service.HandleAccountStatusChangedAsync(
            hunterId.ToString("D"),
            UserAccountStatus.Suspended);

        var second = await service.HandleAccountStatusChangedAsync(
            hunterId.ToString("D"),
            UserAccountStatus.Suspended);

        Assert.Equal(1, first);
        Assert.Equal(0, second);

        Assert.False(await ConsumeAsync(
            store, sessionId, hunterId, submissionId));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task InvalidAccountId_ShouldBeRejected(
        string? accountId)
    {
        var store = new InMemoryHunterCaptureSessionStore();

        var service =
            new HunterAccountCaptureSessionRevocationService(store);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.HandleAccountStatusChangedAsync(
                accountId,
                UserAccountStatus.Suspended));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(999)]
    public async Task InvalidAccountStatus_ShouldBeRejected(
        int statusValue)
    {
        var store = new InMemoryHunterCaptureSessionStore();

        var service =
            new HunterAccountCaptureSessionRevocationService(store);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => service.HandleAccountStatusChangedAsync(
                Guid.NewGuid().ToString("D"),
                (UserAccountStatus)statusValue));
    }

    [Fact]
    public async Task CancelledRequest_ShouldNotRevokeSession()
    {
        var store = new InMemoryHunterCaptureSessionStore();

        var hunterId = Guid.NewGuid();
        var submissionId = Guid.NewGuid();

        var sessionId = await CreateSessionAsync(
            store, hunterId, submissionId);

        var service =
            new HunterAccountCaptureSessionRevocationService(store);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.HandleAccountStatusChangedAsync(
                hunterId.ToString("D"),
                UserAccountStatus.Suspended,
                cts.Token));

        Assert.True(await ConsumeAsync(
            store, sessionId, hunterId, submissionId));
    }

    [Fact]
    public async Task StorageFailure_ShouldPropagate()
    {
        var store = new FailingRevocationStore();

        var service =
            new HunterAccountCaptureSessionRevocationService(store);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.HandleAccountStatusChangedAsync(
                Guid.NewGuid().ToString("D"),
                UserAccountStatus.DeletionRequested));
    }

    [Fact]
    public async Task AlreadyConsumedSession_ShouldNotCountAsRevoked()
    {
        var store = new InMemoryHunterCaptureSessionStore();

        var hunterId = Guid.NewGuid();
        var submissionId = Guid.NewGuid();

        var sessionId = await CreateSessionAsync(
            store, hunterId, submissionId);

        Assert.True(await ConsumeAsync(
            store, sessionId, hunterId, submissionId));

        var service =
            new HunterAccountCaptureSessionRevocationService(store);

        var revoked = await service.HandleAccountStatusChangedAsync(
            hunterId.ToString("D"),
            UserAccountStatus.Suspended);

        Assert.Equal(0, revoked);

        Assert.False(await ConsumeAsync(
            store, sessionId, hunterId, submissionId));
    }

    [Fact]
    public async Task ActiveThenSuspended_ShouldRevokeSession()
    {
        var store = new InMemoryHunterCaptureSessionStore();

        var hunterId = Guid.NewGuid();
        var submissionId = Guid.NewGuid();

        var sessionId = await CreateSessionAsync(
            store, hunterId, submissionId);

        var service =
            new HunterAccountCaptureSessionRevocationService(store);

        Assert.Equal(
            0,
            await service.HandleAccountStatusChangedAsync(
                hunterId.ToString("D"),
                UserAccountStatus.Active));

        Assert.Equal(
            1,
            await service.HandleAccountStatusChangedAsync(
                hunterId.ToString("D"),
                UserAccountStatus.Suspended));

        Assert.False(await ConsumeAsync(
            store, sessionId, hunterId, submissionId));
    }

    [Fact]
    public void NullStore_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            () => new HunterAccountCaptureSessionRevocationService(
                null!));
    }
}
