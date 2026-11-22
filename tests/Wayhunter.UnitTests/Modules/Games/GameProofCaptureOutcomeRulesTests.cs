using System;
using Wayhunter.Application.Modules.Games.Dtos;
using Wayhunter.Application.Modules.Games.Validators;
using Xunit;

namespace Wayhunter.UnitTests.Modules.Games;

public sealed class GameProofCaptureOutcomeRulesTests
{
    private static GameProofSubmissionOperationBinding Valid() => new(Guid.NewGuid(), "player-1", Guid.NewGuid(), Guid.NewGuid(), 1, "capture-1", "provider-1", "media-1");

    [Fact]
    public void MatchingConsumedWithReceipt_ShouldPass()
    {
        var binding = Valid();
        var outcome = new GameProofCaptureOperationOutcome(binding.SubmissionOperationId, GameProofCaptureOperationOutcomeStatus.Consumed, "receipt-1");
        GameProofCaptureOutcomeRules.Validate(binding, outcome);
        Assert.True(GameProofCaptureOutcomeRules.IsConfirmedConsumed(outcome));
    }

    [Fact]
    public void MismatchedOperationId_ShouldReject()
    {
        var binding = Valid();
        var outcome = new GameProofCaptureOperationOutcome(Guid.NewGuid(), GameProofCaptureOperationOutcomeStatus.Consumed, "receipt-1");
        Assert.Throws<InvalidOperationException>(() => GameProofCaptureOutcomeRules.Validate(binding, outcome));
    }

    [Fact]
    public void ConsumedWithoutReceipt_ShouldReject()
    {
        var binding = Valid();
        var outcome = new GameProofCaptureOperationOutcome(binding.SubmissionOperationId, GameProofCaptureOperationOutcomeStatus.Consumed, null);
        Assert.Throws<InvalidOperationException>(() => GameProofCaptureOutcomeRules.Validate(binding, outcome));
    }

    [Fact]
    public void Unknown_ShouldNotBeConfirmed()
    {
        var binding = Valid();
        var outcome = new GameProofCaptureOperationOutcome(binding.SubmissionOperationId, GameProofCaptureOperationOutcomeStatus.Unknown, null);
        GameProofCaptureOutcomeRules.Validate(binding, outcome);
        Assert.False(GameProofCaptureOutcomeRules.IsConfirmedConsumed(outcome));
    }

    [Fact]
    public void Rejected_ShouldNotBeConfirmed()
    {
        var binding = Valid();
        var outcome = new GameProofCaptureOperationOutcome(binding.SubmissionOperationId, GameProofCaptureOperationOutcomeStatus.Rejected, null);
        GameProofCaptureOutcomeRules.Validate(binding, outcome);
        Assert.False(GameProofCaptureOutcomeRules.IsConfirmedConsumed(outcome));
    }

    [Fact]
    public void InvalidOutcomeStatus_ShouldReject()
    {
        var binding = Valid();
        var outcome = new GameProofCaptureOperationOutcome(binding.SubmissionOperationId, (GameProofCaptureOperationOutcomeStatus)99, null);
        Assert.Throws<InvalidOperationException>(() => GameProofCaptureOutcomeRules.Validate(binding, outcome));
    }
}
