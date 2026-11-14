
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Wayhunter.Application.Modules.HunterPlaces.Interfaces;

/// <summary>
/// Contract for backend-managed Hunter capture sessions.
/// A storage implementation must enforce ownership,
/// expiry, and single-use consumption.
/// </summary>
public interface IHunterCaptureSessionStore
{
    /// <summary>
    /// Create and persist a capture session.
    /// The implementation must generate a unique session ID.
    /// Hunter identity and timestamps must be supplied
    /// by trusted backend components.
    /// </summary>
    Task<Guid> CreateAsync(
        Guid hunterId,
        Guid hunterSubmissionId,
        DateTimeOffset issuedAtUtc,
        DateTimeOffset expiresAtUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically consume an existing capture session.
    ///
    /// Return true only if:
    /// - The session exists.
    /// - Hunter and submission IDs match.
    /// - The session is currently valid.
    /// - The session has not been consumed.
    ///
    /// Ownership, expiry, and consumed-state checks
    /// must happen together with the state update.
    /// Return false if the session cannot be consumed.
    /// </summary>
    Task<bool> TryConsumeAsync(
        Guid captureSessionId,
        Guid authenticatedHunterId,
        Guid hunterSubmissionId,
        DateTimeOffset serverReceivedAtUtc,
        CancellationToken cancellationToken = default);
}
