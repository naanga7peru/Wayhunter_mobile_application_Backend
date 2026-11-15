
using System;
using Wayhunter.Domain.Modules.Games.Entities;
using Wayhunter.Domain.Modules.Games.Enums;
using Xunit;

namespace Wayhunter.UnitTests.Modules.Games;

/// <summary>
/// M5-MS05-T05
/// Fail and retry eligibility regression tests.
///
/// Covers existing approved rejection behavior.
///
/// Cancellation and replay-after-terminal-failure
/// require a separate Product Owner decision.
/// </summary>
public sealed class GameFailRetryPolicyTests
{
    private static GameProgress CreateStartedProgress(
        int totalSteps = 3)
    {
        var progress = new GameProgress(
            Guid.NewGuid(),
            totalSteps);

        progress.Start();

        return progress;
    }

    // =====================================
    // TEST 1-2
    // FIRST TWO REJECTIONS ALLOW RESUBMISSION
    // =====================================

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void BelowThreeRejections_ShouldAllowResubmission(
        int rejectionCount)
    {
        var progress = CreateStartedProgress();

        for (int i = 0; i < rejectionCount; i++)
        {
            progress.SubmitEvidence(1);
            progress.RejectEvidence();
        }

        Assert.Equal(
            rejectionCount,
            progress.RejectedEvidenceCount);

        Assert.Equal(
            GameProgressStatus.InProgress,
            progress.Status);

        Assert.Equal(1, progress.CurrentStepNumber);

        // Resubmit only the current stage.
        progress.SubmitEvidence(1);

        Assert.Equal(
            GameProgressStatus.WaitingForVerification,
            progress.Status);
    }

    // =====================================
    // TEST 3
    // THIRD REJECTION CLOSES ATTEMPT
    // =====================================

    [Fact]
    public void ThirdRejection_ShouldPermanentlyCloseAttempt()
    {
        var progress = CreateStartedProgress();

        for (int i = 0; i < 3; i++)
        {
            progress.SubmitEvidence(1);
            progress.RejectEvidence();
        }

        Assert.Equal(3, progress.RejectedEvidenceCount);

        Assert.Equal(
            GameProgressStatus.Rejected,
            progress.Status);

        // Rejected attempt cannot submit again.
        Assert.Throws<InvalidOperationException>(
            () => progress.SubmitEvidence(1));

        // It cannot approve without pending proof.
        Assert.Throws<InvalidOperationException>(
            () => progress.ApproveEvidence());

        // It cannot receive an extra rejection.
        Assert.Throws<InvalidOperationException>(
            () => progress.RejectEvidence());

        Assert.Equal(3, progress.RejectedEvidenceCount);

        Assert.Equal(
            GameProgressStatus.Rejected,
            progress.Status);
    }

    // =====================================
    // TEST 4
    // REJECTION REQUIRES PENDING PROOF
    // =====================================

    [Fact]
    public void RejectWithoutPendingProof_ShouldNotChangeCount()
    {
        var progress = CreateStartedProgress();

        Assert.Throws<InvalidOperationException>(
            () => progress.RejectEvidence());

        Assert.Equal(0, progress.RejectedEvidenceCount);

        Assert.Equal(
            GameProgressStatus.InProgress,
            progress.Status);
    }

    // =====================================
    // TEST 5
    // CANNOT RETRY WHILE VERIFICATION PENDING
    // =====================================

    [Fact]
    public void PendingProof_ShouldBlockDuplicateSubmission()
    {
        var progress = CreateStartedProgress();

        progress.SubmitEvidence(1);

        Assert.Throws<InvalidOperationException>(
            () => progress.SubmitEvidence(1));

        Assert.Equal(
            GameProgressStatus.WaitingForVerification,
            progress.Status);

        Assert.Equal(0, progress.RejectedEvidenceCount);

        Assert.Equal(1, progress.CurrentStepNumber);
    }

    // =====================================
    // TEST 6
    // REJECTION COUNT PERSISTS ACROSS STAGES
    // =====================================

    [Fact]
    public void RejectionsAcrossStages_ShouldCloseAtThree()
    {
        var progress = CreateStartedProgress();

        // Stage 1: first rejection.
        progress.SubmitEvidence(1);
        progress.RejectEvidence();

        Assert.Equal(1, progress.RejectedEvidenceCount);

        // Stage 1: later approved.
        progress.SubmitEvidence(1);
        progress.ApproveEvidence();

        Assert.Equal(2, progress.CurrentStepNumber);

        // Stage 2: second rejection overall.
        progress.SubmitEvidence(2);
        progress.RejectEvidence();

        Assert.Equal(2, progress.RejectedEvidenceCount);

        // Stage 2: third rejection overall.
        progress.SubmitEvidence(2);
        progress.RejectEvidence();

        Assert.Equal(3, progress.RejectedEvidenceCount);

        Assert.Equal(
            GameProgressStatus.Rejected,
            progress.Status);

        // Stage 3 must remain locked.
        Assert.Equal(2, progress.CurrentStepNumber);

        Assert.Throws<InvalidOperationException>(
            () => progress.SubmitEvidence(3));
    }

    // =====================================
    // TEST 7
    // COMPLETED ATTEMPT CANNOT REOPEN
    // =====================================

    [Fact]
    public void CompletedAttempt_ShouldNotAllowRejectionOrRetry()
    {
        var progress = CreateStartedProgress(
            totalSteps: 1);

        progress.SubmitEvidence(1);
        progress.ApproveEvidence();

        Assert.Equal(
            GameProgressStatus.Completed,
            progress.Status);

        Assert.Throws<InvalidOperationException>(
            () => progress.RejectEvidence());

        Assert.Throws<InvalidOperationException>(
            () => progress.SubmitEvidence(1));

        Assert.Equal(
            GameProgressStatus.Completed,
            progress.Status);

        Assert.Equal(0, progress.RejectedEvidenceCount);
    }
}
