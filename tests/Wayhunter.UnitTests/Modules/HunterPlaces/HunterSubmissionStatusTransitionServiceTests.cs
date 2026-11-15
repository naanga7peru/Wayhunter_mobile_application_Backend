
using System;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.HunterPlaces.Interfaces;
using Wayhunter.Application.Modules.HunterPlaces.Services;
using Wayhunter.Domain.Modules.HunterPlaces.Enums;
using Xunit;

namespace Wayhunter.UnitTests.Modules.HunterPlaces;

public sealed class HunterSubmissionStatusTransitionServiceTests
{
    private sealed class RecordingTransitionStore
        : IHunterSubmissionStatusTransitionStore
    {
        public int CallCount { get; private set; }

        public Guid LastSubmissionId { get; private set; }

        public HunterSubmissionStatus LastExpectedStatus
        {
            get;
            private set;
        }

        public HunterSubmissionStatus LastNextStatus
        {
            get;
            private set;
        }

        public CancellationToken LastCancellationToken
        {
            get;
            private set;
        }

        public bool Result { get; set; } = true;

        public Exception? Failure { get; set; }

        public Task<bool> TryTransitionAndRevokeCaptureSessionsAsync(
            Guid submissionId,
            HunterSubmissionStatus expectedStatus,
            HunterSubmissionStatus nextStatus,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            CallCount++;
            LastSubmissionId = submissionId;
            LastExpectedStatus = expectedStatus;
            LastNextStatus = nextStatus;
            LastCancellationToken = cancellationToken;

            if (Failure is not null)
            {
                return Task.FromException<bool>(Failure);
            }

            return Task.FromResult(Result);
        }
    }

    [Fact]
    public async Task SubmitDraft_ShouldRequestAtomicTransition()
    {
        var store = new RecordingTransitionStore();

        var service =
            new HunterSubmissionStatusTransitionService(store);

        var submissionId = Guid.NewGuid();

        var result = await service.SubmitDraftAsync(
            submissionId);

        Assert.True(result);
        Assert.Equal(1, store.CallCount);
        Assert.Equal(submissionId, store.LastSubmissionId);

        Assert.Equal(
            HunterSubmissionStatus.Draft,
            store.LastExpectedStatus);

        Assert.Equal(
            HunterSubmissionStatus.PendingReview,
            store.LastNextStatus);
    }

    [Theory]
    [InlineData(HunterSubmissionStatus.Held)]
    [InlineData(HunterSubmissionStatus.Approved)]
    [InlineData(HunterSubmissionStatus.Rejected)]
    public async Task AdminDecision_ShouldRequestAtomicTransition(
        HunterSubmissionStatus decision)
    {
        var store = new RecordingTransitionStore();

        var service =
            new HunterSubmissionStatusTransitionService(store);

        var submissionId = Guid.NewGuid();

        var result = await service.ApplyAdminDecisionAsync(
            submissionId,
            decision);

        Assert.True(result);
        Assert.Equal(1, store.CallCount);
        Assert.Equal(submissionId, store.LastSubmissionId);

        Assert.Equal(
            HunterSubmissionStatus.PendingReview,
            store.LastExpectedStatus);

        Assert.Equal(
            decision,
            store.LastNextStatus);
    }

    [Theory]
    [InlineData(HunterSubmissionStatus.Draft)]
    [InlineData(HunterSubmissionStatus.PendingReview)]
    [InlineData((HunterSubmissionStatus)999)]
    public async Task InvalidAdminDecision_ShouldBeRejected(
        HunterSubmissionStatus decision)
    {
        var store = new RecordingTransitionStore();

        var service =
            new HunterSubmissionStatusTransitionService(store);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => service.ApplyAdminDecisionAsync(
                Guid.NewGuid(),
                decision));

        Assert.Equal(0, store.CallCount);
    }

    [Fact]
    public async Task EmptySubmissionId_ShouldRejectSubmit()
    {
        var store = new RecordingTransitionStore();

        var service =
            new HunterSubmissionStatusTransitionService(store);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.SubmitDraftAsync(Guid.Empty));

        Assert.Equal(0, store.CallCount);
    }

    [Fact]
    public async Task EmptySubmissionId_ShouldRejectAdminDecision()
    {
        var store = new RecordingTransitionStore();

        var service =
            new HunterSubmissionStatusTransitionService(store);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.ApplyAdminDecisionAsync(
                Guid.Empty,
                HunterSubmissionStatus.Approved));

        Assert.Equal(0, store.CallCount);
    }

    [Fact]
    public async Task StatusConflict_ShouldReturnFalse()
    {
        var store = new RecordingTransitionStore
        {
            Result = false
        };

        var service =
            new HunterSubmissionStatusTransitionService(store);

        var result = await service.SubmitDraftAsync(
            Guid.NewGuid());

        Assert.False(result);
        Assert.Equal(1, store.CallCount);
    }

    [Fact]
    public async Task StorageFailure_ShouldPropagate()
    {
        var store = new RecordingTransitionStore
        {
            Failure = new InvalidOperationException(
                "Simulated transaction failure.")
        };

        var service =
            new HunterSubmissionStatusTransitionService(store);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.SubmitDraftAsync(
                Guid.NewGuid()));

        Assert.Equal(1, store.CallCount);
    }

    [Fact]
    public async Task Cancellation_ShouldStopBeforeStorage()
    {
        var store = new RecordingTransitionStore();

        var service =
            new HunterSubmissionStatusTransitionService(store);

        using var cts = new CancellationTokenSource();

        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.SubmitDraftAsync(
                Guid.NewGuid(),
                cts.Token));

        Assert.Equal(0, store.CallCount);
    }

    [Fact]
    public async Task CancellationToken_ShouldBeForwarded()
    {
        var store = new RecordingTransitionStore();

        var service =
            new HunterSubmissionStatusTransitionService(store);

        using var cts = new CancellationTokenSource();

        await service.SubmitDraftAsync(
            Guid.NewGuid(),
            cts.Token);

        Assert.Equal(
            cts.Token,
            store.LastCancellationToken);
    }

    [Fact]
    public void NullStore_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            () => new HunterSubmissionStatusTransitionService(
                null!));
    }
}
