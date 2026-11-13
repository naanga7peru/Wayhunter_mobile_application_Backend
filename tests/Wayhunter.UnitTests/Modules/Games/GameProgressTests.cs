
using System;
using Wayhunter.Domain.Modules.Games.Entities;
using Wayhunter.Domain.Modules.Games.Enums;
using Xunit;

namespace Wayhunter.UnitTests.Modules.Games;

public sealed class GameProgressTests
{
    private static GameProgress NewProgress(int totalSteps = 3)
    {
        return new GameProgress(Guid.NewGuid(), totalSteps);
    }

    [Fact]
    public void NewProgress_ShouldStartAtStepOne()
    {
        var progress = NewProgress();

        Assert.Equal(1, progress.CurrentStepNumber);
        Assert.Equal(GameProgressStatus.NotStarted, progress.Status);
        Assert.Equal(0, progress.RejectedEvidenceCount);
    }

    [Fact]
    public void Start_ShouldSetInProgress()
    {
        var progress = NewProgress();

        progress.Start();

        Assert.Equal(GameProgressStatus.InProgress, progress.Status);
    }

    [Fact]
    public void CannotStartTwice()
    {
        var progress = NewProgress();
        progress.Start();

        Assert.Throws<InvalidOperationException>(() =>
            progress.Start());
    }

    [Fact]
    public void CannotSkipSteps()
    {
        var progress = NewProgress();
        progress.Start();

        Assert.Throws<InvalidOperationException>(() =>
            progress.SubmitEvidence(2));
    }

    [Fact]
    public void SubmitEvidence_ShouldWaitForVerification()
    {
        var progress = NewProgress();
        progress.Start();

        progress.SubmitEvidence(1);

        Assert.Equal(
            GameProgressStatus.WaitingForVerification,
            progress.Status);
    }

    [Fact]
    public void ApprovedEvidence_ShouldUnlockNextStep()
    {
        var progress = NewProgress();
        progress.Start();
        progress.SubmitEvidence(1);

        progress.ApproveEvidence();

        Assert.Equal(2, progress.CurrentStepNumber);
        Assert.Equal(GameProgressStatus.InProgress, progress.Status);
    }

    [Fact]
    public void LastApprovedStep_ShouldCompleteAttempt()
    {
        var progress = NewProgress(totalSteps: 1);
        progress.Start();
        progress.SubmitEvidence(1);

        progress.ApproveEvidence();

        Assert.Equal(GameProgressStatus.Completed, progress.Status);
    }

    [Fact]
    public void RejectedEvidence_ShouldAllowRetry()
    {
        var progress = NewProgress();
        progress.Start();
        progress.SubmitEvidence(1);

        progress.RejectEvidence();

        Assert.Equal(1, progress.RejectedEvidenceCount);
        Assert.Equal(1, progress.CurrentStepNumber);
        Assert.Equal(GameProgressStatus.InProgress, progress.Status);
    }

    [Fact]
    public void ThreeRejections_ShouldCloseAttempt()
    {
        var progress = NewProgress();
        progress.Start();

        for (int i = 0; i < 3; i++)
        {
            progress.SubmitEvidence(1);
            progress.RejectEvidence();
        }

        Assert.Equal(3, progress.RejectedEvidenceCount);
        Assert.Equal(GameProgressStatus.Rejected, progress.Status);

        Assert.Throws<InvalidOperationException>(() =>
            progress.SubmitEvidence(1));
    }

    [Fact]
    public void CannotApproveWithoutPendingEvidence()
    {
        var progress = NewProgress();
        progress.Start();

        Assert.Throws<InvalidOperationException>(() =>
            progress.ApproveEvidence());
    }

    [Fact]
    public void InvalidTotalSteps_ShouldBeRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new GameProgress(Guid.NewGuid(), 0));
    }
}
