using System;
using Wayhunter.Application.Modules.Games.Dtos;

namespace Wayhunter.Application.Modules.Games.Validators;

public static class GameProofSubmissionOperationRules
{
    public static void Validate(GameProofSubmissionOperationBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);
        if (binding.SubmissionOperationId == Guid.Empty || binding.GameId == Guid.Empty || binding.AttemptId == Guid.Empty || binding.StepNumber < 1)
            throw new ArgumentException("Valid operation, game, attempt and step are required.", nameof(binding));

        if (string.IsNullOrWhiteSpace(binding.AuthenticatedPlayerUserId) || string.IsNullOrWhiteSpace(binding.CaptureSessionId) || string.IsNullOrWhiteSpace(binding.MediaProviderNamespace) || string.IsNullOrWhiteSpace(binding.CanonicalMediaId))
            throw new ArgumentException("Trusted proof identity fields are required.", nameof(binding));

        if (binding.AuthenticatedPlayerUserId.Length > 256)
            throw new ArgumentException("Player user ID exceeds 256 characters.", nameof(binding));

        if (binding.CaptureSessionId.Length > 256)
            throw new ArgumentException("Capture session ID exceeds 256 characters.", nameof(binding));

        if (binding.MediaProviderNamespace.Length > 64)
            throw new ArgumentException("Media provider namespace exceeds 64 characters.", nameof(binding));

        if (binding.CanonicalMediaId.Length > 256)
            throw new ArgumentException("Canonical media ID exceeds 256 characters.", nameof(binding));
    }

    public static void EnsureMatchingStoredOperation(
        GameProofSubmissionOperationSnapshot stored,
        GameProofSubmissionOperationBinding requested)
    {
        ArgumentNullException.ThrowIfNull(stored);
        EnsureMatchingBinding(stored.Binding, requested);
    }

    public static void EnsureMatchingBinding(GameProofSubmissionOperationBinding stored, GameProofSubmissionOperationBinding requested)
    {
        Validate(stored);
        Validate(requested);

        if (stored != requested)
            throw new InvalidOperationException("Submission operation cannot be reused with a different proof binding.");
    }
}
