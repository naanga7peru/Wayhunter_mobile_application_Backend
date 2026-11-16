
using System;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.HunterPlaces.Services;
using Wayhunter.Domain.Modules.HunterPlaces.Entities;
using Wayhunter.Domain.Modules.HunterPlaces.Enums;
using Wayhunter.Infrastructure.Persistence.Repositories.HunterPlaces;
using Xunit;

namespace Wayhunter.UnitTests.Modules.HunterPlaces;

public sealed class InMemoryHunterSubmissionStatusTransitionStoreTests
{
    private static readonly DateTimeOffset IssuedAt =
        new(2026, 11, 15, 10, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset ExpiresAt =
        IssuedAt.AddMinutes(10);

    private static readonly DateTimeOffset ReceivedAt =
        IssuedAt.AddMinutes(5);

    private static InMemoryHunterSubmissionStatusTransitionStore CreateStore(
        Guid hunterId,
        Guid submissionId,
        HunterSubmissionStatus status = HunterSubmissionStatus.Draft)
    {
        return new InMemoryHunterSubmissionStatusTransitionStore(
            new[]
            {
                new HunterSubmission
                {
                    Id = submissionId,
                    UserId = hunterId.ToString("D"),
                    Status = status
                }
            });
    }

    private static Task<Guid> CreateSessionAsync(
        InMemoryHunterSubmissionStatusTransitionStore store,
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
        InMemoryHunterSubmissionStatusTransitionStore store,
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
    public async Task Draft_ShouldPermitValidCapture()
    {
        var hunterId = Guid.NewGuid();
        var submissionId = Guid.NewGuid();

        var store = CreateStore(hunterId, submissionId);

        var sessionId = await CreateSessionAsync(
            store, hunterId, submissionId);

        Assert.True(await ConsumeAsync(
            store, sessionId, hunterId, submissionId));
    }

    [Fact]
    public async Task SubmitDraft_ShouldRevokeAllCaptureSessions()
    {
        var hunterId = Guid.NewGuid();
        var submissionId = Guid.NewGuid();

        var store = CreateStore(hunterId, submissionId);

        var first = await CreateSessionAsync(
            store, hunterId, submissionId);

        var second = await CreateSessionAsync(
            store, hunterId, submissionId);

        var service = new HunterSubmissionStatusTransitionService(store);

        Assert.True(await service.SubmitDraftAsync(submissionId));

        var snapshot = await store.FindBySubmissionIdAsync(
            submissionId);

        Assert.NotNull(snapshot);
        Assert.Equal(
            HunterSubmissionStatus.PendingReview,
            snapshot.Status);

        Assert.False(await ConsumeAsync(
            store, first, hunterId, submissionId));

        Assert.False(await ConsumeAsync(
            store, second, hunterId, submissionId));
    }

    [Fact]
    public async Task NonDraft_ShouldBlockNewCapture()
    {
        var hunterId = Guid.NewGuid();
        var submissionId = Guid.NewGuid();

        var store = CreateStore(hunterId, submissionId);

        var service = new HunterSubmissionStatusTransitionService(store);

        Assert.True(await service.SubmitDraftAsync(submissionId));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            async () => await CreateSessionAsync(
                store, hunterId, submissionId));
    }

    [Fact]
    public async Task DifferentDraft_ShouldRemainUsable()
    {
        var hunterId = Guid.NewGuid();
        var oldId = Guid.NewGuid();
        var newId = Guid.NewGuid();

        var store = new InMemoryHunterSubmissionStatusTransitionStore(
            new[]
            {
                new HunterSubmission
                {
                    Id = oldId,
                    UserId = hunterId.ToString("D")
                },
                new HunterSubmission
                {
                    Id = newId,
                    UserId = hunterId.ToString("D")
                }
            });

        var oldSession = await CreateSessionAsync(
            store, hunterId, oldId);

        var newSession = await CreateSessionAsync(
            store, hunterId, newId);

        var service = new HunterSubmissionStatusTransitionService(store);

        Assert.True(await service.SubmitDraftAsync(oldId));

        Assert.False(await ConsumeAsync(
            store, oldSession, hunterId, oldId));

        Assert.True(await ConsumeAsync(
            store, newSession, hunterId, newId));
    }

    [Fact]
    public async Task WrongOwner_ShouldNotCreateCaptureSession()
    {
        var hunterId = Guid.NewGuid();
        var otherHunterId = Guid.NewGuid();
        var submissionId = Guid.NewGuid();

        var store = CreateStore(hunterId, submissionId);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            async () => await CreateSessionAsync(
                store, otherHunterId, submissionId));
    }

    [Fact]
    public async Task WrongExpectedStatus_ShouldNotChangeSubmission()
    {
        var hunterId = Guid.NewGuid();
        var submissionId = Guid.NewGuid();

        var store = CreateStore(hunterId, submissionId);

        var result =
            await store.TryTransitionAndRevokeCaptureSessionsAsync(
                submissionId,
                HunterSubmissionStatus.PendingReview,
                HunterSubmissionStatus.Approved);

        Assert.False(result);

        var snapshot = await store.FindBySubmissionIdAsync(
            submissionId);

        Assert.NotNull(snapshot);
        Assert.Equal(HunterSubmissionStatus.Draft, snapshot.Status);
    }

    [Theory]
    [InlineData(HunterSubmissionStatus.Held)]
    [InlineData(HunterSubmissionStatus.Approved)]
    [InlineData(HunterSubmissionStatus.Rejected)]
    public async Task AdminDecision_ShouldUpdatePendingSubmission(
        HunterSubmissionStatus decision)
    {
        var hunterId = Guid.NewGuid();
        var submissionId = Guid.NewGuid();

        var store = CreateStore(
            hunterId,
            submissionId,
            HunterSubmissionStatus.PendingReview);

        var service = new HunterSubmissionStatusTransitionService(store);

        Assert.True(await service.ApplyAdminDecisionAsync(
            submissionId, decision));

        var snapshot = await store.FindBySubmissionIdAsync(
            submissionId);

        Assert.NotNull(snapshot);
        Assert.Equal(decision, snapshot.Status);
    }

    [Fact]
    public async Task DuplicateSubmit_ShouldNotApplyTwice()
    {
        var hunterId = Guid.NewGuid();
        var submissionId = Guid.NewGuid();

        var store = CreateStore(hunterId, submissionId);

        var service = new HunterSubmissionStatusTransitionService(store);

        Assert.True(await service.SubmitDraftAsync(submissionId));
        Assert.False(await service.SubmitDraftAsync(submissionId));
    }

    [Fact]
    public async Task InvalidTransition_ShouldKeepDraftUnchanged()
    {
        var hunterId = Guid.NewGuid();
        var submissionId = Guid.NewGuid();

        var store = CreateStore(hunterId, submissionId);

        await Assert.ThrowsAsync<ArgumentException>(
            async () =>
                await store.TryTransitionAndRevokeCaptureSessionsAsync(
                    submissionId,
                    HunterSubmissionStatus.Draft,
                    HunterSubmissionStatus.Approved));

        var snapshot = await store.FindBySubmissionIdAsync(
            submissionId);

        Assert.NotNull(snapshot);
        Assert.Equal(HunterSubmissionStatus.Draft, snapshot.Status);
    }

    [Fact]
    public async Task Cancellation_ShouldNotModifySubmission()
    {
        var hunterId = Guid.NewGuid();
        var submissionId = Guid.NewGuid();

        var store = CreateStore(hunterId, submissionId);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () =>
                await store.TryTransitionAndRevokeCaptureSessionsAsync(
                    submissionId,
                    HunterSubmissionStatus.Draft,
                    HunterSubmissionStatus.PendingReview,
                    cts.Token));

        var snapshot = await store.FindBySubmissionIdAsync(
            submissionId);

        Assert.NotNull(snapshot);
        Assert.Equal(HunterSubmissionStatus.Draft, snapshot.Status);
    }

    [Fact]
    public async Task HunterRevocation_ShouldBlockCaptureConsumption()
    {
        var hunterId = Guid.NewGuid();
        var submissionId = Guid.NewGuid();

        var store = CreateStore(hunterId, submissionId);

        var session = await CreateSessionAsync(
            store, hunterId, submissionId);

        Assert.Equal(
            1,
            await store.RevokeByHunterIdAsync(hunterId));

        Assert.False(await ConsumeAsync(
            store, session, hunterId, submissionId));
    }

    [Fact]
    public async Task ConcurrentSubmissions_ShouldAllowOneTransition()
    {
        var hunterId = Guid.NewGuid();
        var submissionId = Guid.NewGuid();

        var store = CreateStore(hunterId, submissionId);

        var service = new HunterSubmissionStatusTransitionService(store);

        var first = Task.Run(
            () => service.SubmitDraftAsync(submissionId));

        var second = Task.Run(
            () => service.SubmitDraftAsync(submissionId));

        await Task.WhenAll(first, second);

        var results = new[]
        {
            await first,
            await second
        };

        Assert.Single(results, result => result);
    }

    [Fact]
    public async Task ConcurrentConsumeAndTransition_ShouldLeaveNoUsableSession()
    {
        var hunterId = Guid.NewGuid();
        var submissionId = Guid.NewGuid();

        var store = CreateStore(hunterId, submissionId);

        var sessionId = await CreateSessionAsync(
            store, hunterId, submissionId);

        var service = new HunterSubmissionStatusTransitionService(store);

        var transitionTask = Task.Run(
            () => service.SubmitDraftAsync(submissionId));

        var consumeTask = Task.Run(
            () => ConsumeAsync(
                store, sessionId, hunterId, submissionId));

        await Task.WhenAll(transitionTask, consumeTask);

        Assert.True(await transitionTask);

        // The first consume may win the race.
        // Once both operations finish, reuse is impossible.
        Assert.False(await ConsumeAsync(
            store, sessionId, hunterId, submissionId));

        var snapshot = await store.FindBySubmissionIdAsync(
            submissionId);

        Assert.NotNull(snapshot);
        Assert.Equal(
            HunterSubmissionStatus.PendingReview,
            snapshot.Status);
    }

    [Fact]
    public async Task MissingSubmission_ShouldReturnFalse()
    {
        var store = CreateStore(
            Guid.NewGuid(), Guid.NewGuid());

        var result =
            await store.TryTransitionAndRevokeCaptureSessionsAsync(
                Guid.NewGuid(),
                HunterSubmissionStatus.Draft,
                HunterSubmissionStatus.PendingReview);

        Assert.False(result);
    }
}
