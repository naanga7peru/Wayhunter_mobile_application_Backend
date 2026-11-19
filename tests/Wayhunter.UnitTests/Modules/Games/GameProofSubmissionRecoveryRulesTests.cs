using System;
using Wayhunter.Application.Modules.Games.Dtos;
using Wayhunter.Application.Modules.Games.Validators;
using Xunit;

namespace Wayhunter.UnitTests.Modules.Games;

public sealed class GameProofSubmissionRecoveryRulesTests
{
    [Theory]
    [InlineData(GameProofSubmissionOperationState.VerificationInFlight)]
    [InlineData(GameProofSubmissionOperationState.ProviderOutcomeUnknown)]
    [InlineData(GameProofSubmissionOperationState.CaptureConsumed)]
    public void UncertainOrConsumedState_RequiresReconciliation(GameProofSubmissionOperationState state)
    {
        Assert.True(GameProofSubmissionRecoveryRules.RequiresReconciliation(state));
    }

    [Theory]
    [InlineData(GameProofSubmissionOperationState.Pending)]
    [InlineData(GameProofSubmissionOperationState.Completed)]
    [InlineData(GameProofSubmissionOperationState.Rejected)]
    [InlineData(GameProofSubmissionOperationState.ManualReviewRequired)]
    public void OtherStates_DoNotRequireAutomaticReconciliation(GameProofSubmissionOperationState state)
    {
        Assert.False(GameProofSubmissionRecoveryRules.RequiresReconciliation(state));
    }

    [Theory]
    [InlineData(GameProofSubmissionOperationState.Completed)]
    [InlineData(GameProofSubmissionOperationState.Rejected)]
    [InlineData(GameProofSubmissionOperationState.ManualReviewRequired)]
    public void TerminalStates_ShouldBeIdentified(GameProofSubmissionOperationState state)
    {
        Assert.True(GameProofSubmissionRecoveryRules.IsTerminal(state));
    }

    [Fact]
    public void Pending_ShouldAllowInitialClaim()
    {
        GameProofSubmissionRecoveryRules.EnsureEligibleForInitialClaim(GameProofSubmissionOperationState.Pending);
    }

    [Theory]
    [InlineData(GameProofSubmissionOperationState.VerificationInFlight)]
    [InlineData(GameProofSubmissionOperationState.ProviderOutcomeUnknown)]
    [InlineData(GameProofSubmissionOperationState.CaptureConsumed)]
    [InlineData(GameProofSubmissionOperationState.Completed)]
    [InlineData(GameProofSubmissionOperationState.Rejected)]
    [InlineData(GameProofSubmissionOperationState.ManualReviewRequired)]
    public void NonPendingState_ShouldRejectInitialClaim(GameProofSubmissionOperationState state)
    {
        Assert.Throws<InvalidOperationException>(() => GameProofSubmissionRecoveryRules.EnsureEligibleForInitialClaim(state));
    }
    [Fact]
    public void AllowedTransitions_ShouldPass()
    {
        var transitions = new[] {
            (GameProofSubmissionOperationState.Pending, GameProofSubmissionOperationState.VerificationInFlight),
            (GameProofSubmissionOperationState.VerificationInFlight, GameProofSubmissionOperationState.ProviderOutcomeUnknown),
            (GameProofSubmissionOperationState.VerificationInFlight, GameProofSubmissionOperationState.CaptureConsumed),
            (GameProofSubmissionOperationState.ProviderOutcomeUnknown, GameProofSubmissionOperationState.CaptureConsumed),
            (GameProofSubmissionOperationState.CaptureConsumed, GameProofSubmissionOperationState.Completed)
        };
        foreach (var (from, to) in transitions)
            Assert.True(GameProofSubmissionRecoveryRules.IsAllowedTransition(from, to));
    }

    [Fact]
    public void UnsafeTransitions_ShouldReject()
    {
        var transitions = new[] {
            (GameProofSubmissionOperationState.ProviderOutcomeUnknown, GameProofSubmissionOperationState.VerificationInFlight),
            (GameProofSubmissionOperationState.CaptureConsumed, GameProofSubmissionOperationState.VerificationInFlight),
            (GameProofSubmissionOperationState.Completed, GameProofSubmissionOperationState.Pending),
            (GameProofSubmissionOperationState.Pending, GameProofSubmissionOperationState.Completed)
        };
        foreach (var (from, to) in transitions)
            Assert.False(GameProofSubmissionRecoveryRules.IsAllowedTransition(from, to));
    }

    [Theory]
    [InlineData(GameProofSubmissionOperationState.Completed)]
    [InlineData(GameProofSubmissionOperationState.Rejected)]
    [InlineData(GameProofSubmissionOperationState.ManualReviewRequired)]
    public void TerminalState_ShouldRejectEveryTransition(GameProofSubmissionOperationState state)
    {
        foreach (GameProofSubmissionOperationState next in Enum.GetValues<GameProofSubmissionOperationState>())
            Assert.False(GameProofSubmissionRecoveryRules.IsAllowedTransition(state, next));
    }
}
