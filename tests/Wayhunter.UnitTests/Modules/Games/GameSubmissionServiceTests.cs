
using System;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.Games.Dtos;
using Wayhunter.Application.Modules.Games.Interfaces;
using Wayhunter.Application.Modules.Games.Services;
using Wayhunter.Application.Modules.IdentitySocial.Dtos;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Xunit;

namespace Wayhunter.UnitTests.Modules.Games;

public sealed class GameSubmissionServiceTests
{
    private static readonly DateTimeOffset CreatedAt =
        new(2026, 11, 14, 5, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset SubmittedAt =
        CreatedAt.AddHours(2);

    // =====================================
    // FAKE SUBMISSION STORE - NO SQL
    // =====================================

    private sealed class FakeSubmissionStore
        : IGameSubmissionStore
    {
        public int CallCount { get; private set; }

        public ApprovalIdentitySnapshotDto? LastIdentity
        { get; private set; }

        public CancellationToken LastCancellationToken
        { get; private set; }

        public bool SimulateFailure { get; set; }

        public Task<GameAdminQueueItemDto>
            SubmitDraftAndEnqueueAsync(
                ApprovalIdentitySnapshotDto identity,
                CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            CallCount++;
            LastIdentity = identity;
            LastCancellationToken = cancellationToken;

            if (SimulateFailure)
            {
                throw new InvalidOperationException(
                    "Simulated storage failure.");
            }

            // Fake response only.
            // This does NOT persist a real queue item.
            var result = new GameAdminQueueItemDto(
                identity.ItemId,
                identity.UserId,
                identity.DisplayNameSnapshot,
                "Jaffna Treasure Hunt",
                identity.ServerTimestampUtc);

            return Task.FromResult(result);
        }
    }

    // =====================================
    // CREATE ACTIVE USER
    // =====================================

    private static UserAccount NewActiveUser()
    {
        var user = UserAccount.RegisterPending(
            "Thuvarathan",
            "creator@example.com",
            "test-password-hash",
            CreatedAt);

        user.ActivateAfterVerifiedOtp(
            CreatedAt.AddMinutes(10));

        return user;
    }

    // =====================================
    // TEST 01 - VALID SUBMISSION
    // =====================================

    [Fact]
    public async Task ActiveCreator_ShouldSubmitSuccessfully()
    {
        var store = new FakeSubmissionStore();
        var service = new GameSubmissionService(store);
        var creator = NewActiveUser();
        var gameId = Guid.NewGuid();

        var result = await service.SubmitAsync(
            gameId,
            creator,
            SubmittedAt);

        Assert.Equal(1, store.CallCount);
        Assert.Equal(gameId, result.GameId);

        Assert.Equal(
            creator.UserId,
            result.CreatorUserId);

        Assert.Equal(
            "Thuvarathan",
            result.CreatorNameSnapshot);

        Assert.Equal(
            SubmittedAt,
            result.SubmittedAtUtc);
    }

    // =====================================
    // TEST 02 - IDENTITY SNAPSHOT
    // =====================================

    [Fact]
    public async Task Submission_ShouldPassIdentitySnapshot()
    {
        var store = new FakeSubmissionStore();
        var service = new GameSubmissionService(store);
        var creator = NewActiveUser();
        var gameId = Guid.NewGuid();

        await service.SubmitAsync(
            gameId,
            creator,
            SubmittedAt);

        var snapshot = Assert.IsType<
            ApprovalIdentitySnapshotDto>(
            store.LastIdentity);

        Assert.Equal(gameId, snapshot.ItemId);
        Assert.Equal(creator.UserId, snapshot.UserId);

        Assert.Equal(
            "Thuvarathan",
            snapshot.DisplayNameSnapshot);

        Assert.Equal(
            SubmittedAt,
            snapshot.ServerTimestampUtc);
    }

    // =====================================
    // TEST 03 - UNVERIFIED CREATOR
    // =====================================

    [Fact]
    public async Task UnverifiedCreator_ShouldBeRejected()
    {
        var store = new FakeSubmissionStore();
        var service = new GameSubmissionService(store);

        var creator = UserAccount.RegisterPending(
            "Pending Creator",
            "pending@example.com",
            "test-password-hash",
            CreatedAt);

        await Assert.ThrowsAsync<
            InvalidOperationException>(
            () => service.SubmitAsync(
                Guid.NewGuid(),
                creator,
                SubmittedAt));

        Assert.Equal(0, store.CallCount);
    }

    // =====================================
    // TEST 04 - SUSPENDED CREATOR
    // =====================================

    [Fact]
    public async Task SuspendedCreator_ShouldBeRejected()
    {
        var store = new FakeSubmissionStore();
        var service = new GameSubmissionService(store);
        var creator = NewActiveUser();

        creator.Suspend();

        await Assert.ThrowsAsync<
            InvalidOperationException>(
            () => service.SubmitAsync(
                Guid.NewGuid(),
                creator,
                SubmittedAt));

        Assert.Equal(0, store.CallCount);
    }

    // =====================================
    // TEST 05 - INVALID GAME ID
    // =====================================

    [Fact]
    public async Task EmptyGameId_ShouldBeRejected()
    {
        var store = new FakeSubmissionStore();
        var service = new GameSubmissionService(store);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.SubmitAsync(
                Guid.Empty,
                NewActiveUser(),
                SubmittedAt));

        Assert.Equal(0, store.CallCount);
    }

    // =====================================
    // TEST 06 - MISSING TIMESTAMP
    // =====================================

    [Fact]
    public async Task MissingTimestamp_ShouldBeRejected()
    {
        var store = new FakeSubmissionStore();
        var service = new GameSubmissionService(store);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.SubmitAsync(
                Guid.NewGuid(),
                NewActiveUser(),
                default));

        Assert.Equal(0, store.CallCount);
    }

    // =====================================
    // TEST 07 - NON-UTC TIMESTAMP
    // =====================================

    [Fact]
    public async Task NonUtcTimestamp_ShouldBeRejected()
    {
        var store = new FakeSubmissionStore();
        var service = new GameSubmissionService(store);

        var nonUtcTime = new DateTimeOffset(
            2026, 11, 14, 12, 0, 0,
            TimeSpan.FromMinutes(330));

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.SubmitAsync(
                Guid.NewGuid(),
                NewActiveUser(),
                nonUtcTime));

        Assert.Equal(0, store.CallCount);
    }

    // =====================================
    // TEST 08 - NULL CREATOR
    // =====================================

    [Fact]
    public async Task NullCreator_ShouldBeRejected()
    {
        var store = new FakeSubmissionStore();
        var service = new GameSubmissionService(store);

        await Assert.ThrowsAsync<
            ArgumentNullException>(
            () => service.SubmitAsync(
                Guid.NewGuid(),
                null!,
                SubmittedAt));

        Assert.Equal(0, store.CallCount);
    }

    // =====================================
    // TEST 09 - MISSING STORE
    // =====================================

    [Fact]
    public void NullStore_ShouldBeRejected()
    {
        Assert.Throws<ArgumentNullException>(
            () => new GameSubmissionService(null!));
    }

    // =====================================
    // TEST 10 - CANCELLATION TOKEN
    // =====================================

    [Fact]
    public async Task CancellationToken_ShouldBeForwarded()
    {
        var store = new FakeSubmissionStore();
        var service = new GameSubmissionService(store);

        using var cancellation =
            new CancellationTokenSource();

        await service.SubmitAsync(
            Guid.NewGuid(),
            NewActiveUser(),
            SubmittedAt,
            cancellation.Token);

        Assert.Equal(
            cancellation.Token,
            store.LastCancellationToken);
    }

    // =====================================
    // TEST 11 - CANCELLED REQUEST
    // =====================================

    [Fact]
    public async Task CancelledRequest_ShouldNotCallStore()
    {
        var store = new FakeSubmissionStore();
        var service = new GameSubmissionService(store);

        using var cancellation =
            new CancellationTokenSource();

        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<
            OperationCanceledException>(
            () => service.SubmitAsync(
                Guid.NewGuid(),
                NewActiveUser(),
                SubmittedAt,
                cancellation.Token));

        Assert.Equal(0, store.CallCount);
    }

    // =====================================
    // TEST 12 - STORE FAILURE
    // =====================================

    [Fact]
    public async Task StoreFailure_ShouldBePropagated()
    {
        var store = new FakeSubmissionStore
        {
            SimulateFailure = true
        };

        var service = new GameSubmissionService(store);

        await Assert.ThrowsAsync<
            InvalidOperationException>(
            () => service.SubmitAsync(
                Guid.NewGuid(),
                NewActiveUser(),
                SubmittedAt));

        Assert.Equal(1, store.CallCount);
    }
}
