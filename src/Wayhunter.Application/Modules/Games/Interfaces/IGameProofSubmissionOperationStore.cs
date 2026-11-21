using System;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.Games.Dtos;

namespace Wayhunter.Application.Modules.Games.Interfaces;

/// <summary>
/// Proposed isolated durable M5 proof operation store contract.
/// Implementations must enforce atomic registration and claims.
/// Provider reconciliation and production wiring need separate approval.
/// </summary>
public interface IGameProofSubmissionOperationStore
{
    Task<GameProofSubmissionOperationSnapshot> RegisterOrReadAsync(
        GameProofSubmissionOperationBinding binding,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default);

    Task<bool> TryClaimPendingAsync(
        GameProofSubmissionOperationBinding binding,
        string claimToken,
        DateTimeOffset leaseExpiresAtUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically claims recovery for the same immutable operation binding.
    /// Store must verify eligible state, expiry and exclusive lease ownership.
    /// Claiming recovery never authorizes blind provider consumption retry.
    /// </summary>
    Task<bool> TryClaimReconciliationAsync(
        GameProofSubmissionOperationBinding binding,
        string recoveryClaimToken,
        DateTimeOffset nowUtc,
        DateTimeOffset leaseExpiresAtUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Records an uncertain provider outcome only for the current lease owner.
    /// Implementation must atomically verify immutable binding, expected
    /// claim token, unexpired SQL-server lease and VerificationInFlight state.
    /// Unknown outcome never authorizes another consumption attempt.
    /// </summary>
    Task<bool> TryMarkProviderOutcomeUnknownAsync(
        GameProofSubmissionOperationBinding binding,
        string expectedClaimToken,
        CancellationToken cancellationToken = default);


    /// <summary>
    /// Records a trusted provider's confirmed capture consumption
    /// for the current operation and valid SQL claim.
    ///
    /// Does not invoke the capture provider or complete
    /// game evidence and progress persistence.
    /// Production activation requires separate approval.
    /// </summary>
    Task<bool> TryRecordConfirmedConsumptionAsync(
        GameProofSubmissionOperationBinding binding,
        string expectedClaimToken,
        GameProofCaptureOperationOutcome outcome,
        CancellationToken cancellationToken = default);


    Task<GameProofSubmissionOperationSnapshot?> GetAsync(
        Guid submissionOperationId,
        CancellationToken cancellationToken = default);
}
