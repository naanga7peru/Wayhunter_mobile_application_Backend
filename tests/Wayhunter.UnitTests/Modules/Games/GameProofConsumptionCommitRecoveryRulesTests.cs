
using System;
using Wayhunter.Application.Modules.Games.Dtos;
using Wayhunter.Application.Modules.Games.Validators;
using Xunit;

namespace Wayhunter.UnitTests.Modules.Games;

public sealed class GameProofConsumptionCommitRecoveryRulesTests
{
    private static GameProofSubmissionOperationBinding Binding() =>
        new(
            Guid.NewGuid(),
            "player-1",
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            "capture-1",
            "provider-1",
            "media-1");

    private static GameProofSubmissionOperationSnapshot Snapshot(
        GameProofSubmissionOperationBinding binding,
        GameProofSubmissionOperationState state =
            GameProofSubmissionOperationState.CaptureConsumed,
        string? receipt = "receipt-1",
        bool confirmed = true,
        Guid? evidenceId = null)
    {
        var now = DateTimeOffset.UtcNow;

        return new(
            binding,
            state,
            evidenceId,
            null,
            null,
            confirmed ? now : null,
            null,
            0,
            null,
            now,
            now,
            receipt);
    }

    [Fact]
    public void ConfirmedReceipt_ShouldRecover()
    {
        var binding = Binding();

        Assert.True(
            GameProofConsumptionCommitRecoveryRules.IsDurablyConfirmed(
                Snapshot(binding),
                binding,
                "receipt-1"));
    }

    [Fact]
    public void CompletedWithEvidence_ShouldRecover()
    {
        var binding = Binding();

        Assert.True(
            GameProofConsumptionCommitRecoveryRules.IsDurablyConfirmed(
                Snapshot(
                    binding,
                    GameProofSubmissionOperationState.Completed,
                    evidenceId: Guid.NewGuid()),
                binding,
                "receipt-1"));
    }

    [Fact]
    public void CompletedWithoutEvidence_ShouldFailClosed()
    {
        var binding = Binding();

        Assert.False(
            GameProofConsumptionCommitRecoveryRules.IsDurablyConfirmed(
                Snapshot(
                    binding,
                    GameProofSubmissionOperationState.Completed),
                binding,
                "receipt-1"));
    }

    [Fact]
    public void MissingSnapshot_ShouldFailClosed()
    {
        var binding = Binding();

        Assert.False(
            GameProofConsumptionCommitRecoveryRules.IsDurablyConfirmed(
                null, binding, "receipt-1"));
    }

    [Theory]
    [InlineData(GameProofSubmissionOperationState.Pending)]
    [InlineData(GameProofSubmissionOperationState.VerificationInFlight)]
    [InlineData(GameProofSubmissionOperationState.ProviderOutcomeUnknown)]
    [InlineData(GameProofSubmissionOperationState.Rejected)]
    [InlineData(GameProofSubmissionOperationState.ManualReviewRequired)]
    public void OtherStates_ShouldFailClosed(
        GameProofSubmissionOperationState state)
    {
        var binding = Binding();

        Assert.False(
            GameProofConsumptionCommitRecoveryRules.IsDurablyConfirmed(
                Snapshot(binding, state),
                binding,
                "receipt-1"));
    }

    [Fact]
    public void DifferentReceipt_ShouldFailClosed()
    {
        var binding = Binding();

        Assert.False(
            GameProofConsumptionCommitRecoveryRules.IsDurablyConfirmed(
                Snapshot(binding),
                binding,
                "different-receipt"));
    }

    [Fact]
    public void ReceiptComparison_ShouldBeCaseSensitive()
    {
        var binding = Binding();

        Assert.False(
            GameProofConsumptionCommitRecoveryRules.IsDurablyConfirmed(
                Snapshot(binding),
                binding,
                "RECEIPT-1"));
    }

    [Fact]
    public void MissingConfirmationTimestamp_ShouldFailClosed()
    {
        var binding = Binding();

        Assert.False(
            GameProofConsumptionCommitRecoveryRules.IsDurablyConfirmed(
                Snapshot(binding, confirmed: false),
                binding,
                "receipt-1"));
    }

    [Fact]
    public void ConflictingBinding_ShouldReject()
    {
        var binding = Binding();
        var changed = binding with
        {
            CanonicalMediaId = "different-media"
        };

        Assert.Throws<InvalidOperationException>(() =>
            GameProofConsumptionCommitRecoveryRules.IsDurablyConfirmed(
                Snapshot(binding),
                changed,
                "receipt-1"));
    }
}
