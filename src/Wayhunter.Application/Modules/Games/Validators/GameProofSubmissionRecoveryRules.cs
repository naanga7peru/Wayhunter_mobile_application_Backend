using System;
using Wayhunter.Application.Modules.Games.Dtos;

namespace Wayhunter.Application.Modules.Games.Validators;

/// <summary>
/// Isolated M5 recovery state guards.
/// These guards do not replace atomic durable claims or trusted provider reconciliation.
/// </summary>
public static class GameProofSubmissionRecoveryRules
{
    public static bool RequiresReconciliation(GameProofSubmissionOperationState state) =>
        state is GameProofSubmissionOperationState.VerificationInFlight
            or GameProofSubmissionOperationState.ProviderOutcomeUnknown
            or GameProofSubmissionOperationState.CaptureConsumed;

    public static bool IsTerminal(GameProofSubmissionOperationState state) =>
        state is GameProofSubmissionOperationState.Completed
            or GameProofSubmissionOperationState.Rejected
            or GameProofSubmissionOperationState.ManualReviewRequired;

    public static void EnsureEligibleForInitialClaim(GameProofSubmissionOperationState state)
    {
        if (state != GameProofSubmissionOperationState.Pending)
            throw new InvalidOperationException("Operation cannot be claimed for initial verification.");
    }
    /// <summary>
    /// State-only transition policy. Trusted provider evidence, durable
    /// claims and SQL commit verification must be enforced separately.
    /// </summary>
    public static bool IsAllowedTransition(GameProofSubmissionOperationState current, GameProofSubmissionOperationState next) => current switch
    {
        GameProofSubmissionOperationState.Pending => next == GameProofSubmissionOperationState.VerificationInFlight,
        GameProofSubmissionOperationState.VerificationInFlight => next is GameProofSubmissionOperationState.ProviderOutcomeUnknown or GameProofSubmissionOperationState.CaptureConsumed or GameProofSubmissionOperationState.Rejected or GameProofSubmissionOperationState.ManualReviewRequired,
        GameProofSubmissionOperationState.ProviderOutcomeUnknown => next is GameProofSubmissionOperationState.CaptureConsumed or GameProofSubmissionOperationState.Rejected or GameProofSubmissionOperationState.ManualReviewRequired,
        GameProofSubmissionOperationState.CaptureConsumed => next is GameProofSubmissionOperationState.Completed or GameProofSubmissionOperationState.ManualReviewRequired,
        _ => false
    };
}
