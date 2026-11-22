using System;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.Games.Dtos;

namespace Wayhunter.Application.Modules.Games.Interfaces;

/// <summary>
/// Proposed M5 trusted capture provider recovery contract.
/// Requires Capture Owner approval before implementation or production use.
/// Provider must enforce operation-bound single-use consumption.
/// Unknown outcomes must never trigger blind consumption retries.
/// </summary>
public interface IGameProofCaptureRecoveryProvider
{
    Task<GameProofCaptureOperationOutcome> VerifyAndConsumeOnceAsync(
        GameProofSubmissionOperationBinding binding,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default);

    Task<GameProofCaptureOperationOutcome> GetOperationOutcomeAsync(
        GameProofSubmissionOperationBinding binding,
        CancellationToken cancellationToken = default);
}
