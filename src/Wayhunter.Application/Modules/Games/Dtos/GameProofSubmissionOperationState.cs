namespace Wayhunter.Application.Modules.Games.Dtos;

/// <summary>
/// Proposed isolated M5 proof recovery processing states.
/// Provider outcomes require trusted reconciliation before retry.
/// No production activation is authorized.
/// </summary>
public enum GameProofSubmissionOperationState
{
    Pending = 0,
    VerificationInFlight = 1,
    ProviderOutcomeUnknown = 2,
    CaptureConsumed = 3,
    Completed = 4,
    Rejected = 5,
    ManualReviewRequired = 6
}
