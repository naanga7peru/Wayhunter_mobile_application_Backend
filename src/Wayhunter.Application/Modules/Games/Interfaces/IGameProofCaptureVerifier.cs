
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Wayhunter.Application.Modules.Games.Interfaces;

/// <summary>
/// M5-MS05-T02
/// Trusted verification boundary for Game stage proof.
///
/// A real implementation must check the backend's
/// capture-session record, not a client-provided flag.
/// </summary>
public interface IGameProofCaptureVerifier
{
    /// <summary>
    /// Verify and consume a capture session only if:
    /// - It belongs to the authenticated player.
    /// - It belongs to the requested Game and attempt.
    /// - It matches the current stage and media.
    /// - It has not expired or been consumed.
    /// - The media is associated with a trusted
    ///   camera capture workflow.
    ///
    /// Return false when verification fails.
    ///
    /// Consumption must be single-use and atomic
    /// in the trusted capture-session store.
    /// </summary>
    Task<bool> VerifyAndConsumeAsync(
        string captureSessionId,
        string mediaId,
        Guid gameId,
        Guid attemptId,
        int stepNumber,
        string authenticatedPlayerUserId,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default);
}
