
using System;
using Wayhunter.Application.Modules.Games.Dtos;

namespace Wayhunter.Application.Modules.Games.Validators;

public static class GameProofConsumptionCommitRecoveryRules
{
    public static bool IsDurablyConfirmed(
        GameProofSubmissionOperationSnapshot? stored,
        GameProofSubmissionOperationBinding requested,
        string expectedReceiptId)
    {
        GameProofSubmissionOperationRules.Validate(requested);

        if (string.IsNullOrWhiteSpace(expectedReceiptId) ||
            expectedReceiptId.Length > 256)
        {
            throw new ArgumentException(
                "Valid provider receipt is required.",
                nameof(expectedReceiptId));
        }

        if (stored is null)
            return false;

        GameProofSubmissionOperationRules
            .EnsureMatchingStoredOperation(stored, requested);

        if (!string.Equals(
                stored.ProviderReceiptId,
                expectedReceiptId,
                StringComparison.Ordinal))
        {
            return false;
        }

        if (stored.ProviderConsumptionConfirmedAtUtc is null)
            return false;

        return stored.State switch
        {
            GameProofSubmissionOperationState.CaptureConsumed
                => true,

            GameProofSubmissionOperationState.Completed
                => stored.EvidenceId.HasValue,

            _ => false
        };
    }
}
