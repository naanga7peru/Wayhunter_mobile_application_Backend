using System;
using Wayhunter.Application.Modules.Games.Dtos;

namespace Wayhunter.Application.Modules.Games.Validators;

/// <summary>
/// Isolated M5 provider outcome validation.
/// Does not replace trusted provider authentication or binding verification.
/// </summary>
public static class GameProofCaptureOutcomeRules
{
    public static void Validate(GameProofSubmissionOperationBinding binding, GameProofCaptureOperationOutcome outcome)
    {
        GameProofSubmissionOperationRules.Validate(binding);
        ArgumentNullException.ThrowIfNull(outcome);

        if (outcome.SubmissionOperationId != binding.SubmissionOperationId)
            throw new InvalidOperationException("Provider outcome belongs to another operation.");

        if (!Enum.IsDefined(outcome.Status))
            throw new InvalidOperationException("Unsupported capture provider outcome.");

        if (outcome.ProviderReceiptId is { Length: > 256 })
            throw new InvalidOperationException("Provider receipt exceeds 256 characters.");

        if (outcome.Status == GameProofCaptureOperationOutcomeStatus.Consumed && string.IsNullOrWhiteSpace(outcome.ProviderReceiptId))
            throw new InvalidOperationException("Confirmed consumption requires a provider receipt.");
    }

    public static bool IsConfirmedConsumed(GameProofCaptureOperationOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        return outcome.Status == GameProofCaptureOperationOutcomeStatus.Consumed && !string.IsNullOrWhiteSpace(outcome.ProviderReceiptId);
    }
}
