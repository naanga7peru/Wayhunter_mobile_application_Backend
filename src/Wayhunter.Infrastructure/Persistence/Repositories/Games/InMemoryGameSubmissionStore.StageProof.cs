
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.Games.Dtos;
using Wayhunter.Application.Modules.Games.Interfaces;
using Wayhunter.Domain.Modules.Games.Entities;
using Wayhunter.Domain.Modules.Games.Services;

namespace Wayhunter.Infrastructure.Persistence.Repositories.Games;

/// <summary>
/// M5-MS05-T02
/// In-memory Game stage proof submission.
///
/// Prototype only. SQL persistence and production
/// capture-session integration remain pending.
/// </summary>
public sealed partial class InMemoryGameSubmissionStore
    : ISubmitGameStageProofStore
{
    private readonly SemaphoreSlim _proofSubmissionGate =
        new(1, 1);

    private readonly Dictionary<Guid, GameEvidence>
        _submittedGameProofs = new();

    private readonly IGameProofCaptureVerifier?
        _gameProofCaptureVerifier;

    // Preserve existing usage by other Games tests.
    public InMemoryGameSubmissionStore()
    {
    }

    // Proof submission requires a trusted verifier.
    public InMemoryGameSubmissionStore(
        IGameProofCaptureVerifier captureVerifier)
    {
        _gameProofCaptureVerifier =
            captureVerifier ??
            throw new ArgumentNullException(
                nameof(captureVerifier));
    }

    public async Task<GameEvidence> SubmitCurrentStageProofAsync(
        SubmitGameStageProofCommand command,
        string playerUserId,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        cancellationToken.ThrowIfCancellationRequested();

        if (command.GameId == Guid.Empty ||
            command.AttemptId == Guid.Empty ||
            command.StepNumber <= 0 ||
            string.IsNullOrWhiteSpace(command.MediaId) ||
            string.IsNullOrWhiteSpace(command.CaptureSessionId))
        {
            throw new ArgumentException(
                "Invalid Game stage proof request.",
                nameof(command));
        }

        if (string.IsNullOrWhiteSpace(playerUserId))
        {
            throw new ArgumentException(
                "Authenticated player ID is required.",
                nameof(playerUserId));
        }

        if (nowUtc == default ||
            nowUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "Valid server UTC time is required.",
                nameof(nowUtc));
        }

        // No verifier = no proof submission.
        // Never allow a client flag to bypass this.
        if (_gameProofCaptureVerifier is null)
        {
            throw new InvalidOperationException(
                "Trusted Game capture verification is not configured.");
        }

        string cleanPlayerId = playerUserId.Trim();
        string cleanMediaId = command.MediaId.Trim();
        string cleanSessionId = command.CaptureSessionId.Trim();

        var evidence = new GameEvidence(
            Guid.NewGuid(),
            command.AttemptId,
            command.StepNumber,
            cleanMediaId,
            nowUtc);

        // Serialize submissions within this in-memory
        // prototype to prevent concurrent proof races.
        await _proofSubmissionGate.WaitAsync(
            cancellationToken);

        try
        {
            // Pre-check before consuming capture session.
            lock (_gate)
            {
                cancellationToken.ThrowIfCancellationRequested();

                ValidateStageProof(
                    command,
                    cleanPlayerId,
                    nowUtc,
                    evidence);
            }

            // This must be implemented by a trusted
            // backend capture-session provider.
            bool verified =
                await _gameProofCaptureVerifier
                    .VerifyAndConsumeAsync(
                        cleanSessionId,
                        cleanMediaId,
                        command.GameId,
                        command.AttemptId,
                        command.StepNumber,
                        cleanPlayerId,
                        nowUtc,
                        cancellationToken);

            if (!verified)
            {
                throw new UnauthorizedAccessException(
                    "Game camera proof verification failed.");
            }

            // Recheck because Game state may have changed
            // while awaiting the capture verifier.
            lock (_gate)
            {
                var progress = ValidateStageProof(
                    command,
                    cleanPlayerId,
                    nowUtc,
                    evidence);

                // SubmitEvidence changes progress to
                // WaitingForVerification.
                // It does NOT unlock the next stage.
                progress.SubmitEvidence(
                    command.StepNumber);

                _submittedGameProofs.Add(
                    evidence.Id,
                    evidence);

                return evidence;
            }
        }
        finally
        {
            _proofSubmissionGate.Release();
        }
    }

    private GameProgress ValidateStageProof(
        SubmitGameStageProofCommand command,
        string playerUserId,
        DateTimeOffset nowUtc,
        GameEvidence evidence)
    {
        // Caller must hold _gate.

        if (!_games.TryGetValue(
            command.GameId,
            out var game))
        {
            throw new KeyNotFoundException(
                "Game not found.");
        }

        if (!game.ApprovedAtUtc.HasValue ||
            game.ApprovedAtUtc.Value > nowUtc)
        {
            throw new InvalidOperationException(
                "Game approval is not effective.");
        }

        var key = (
            command.GameId,
            playerUserId);

        if (!_gameAttempts.TryGetValue(
            key,
            out var stored))
        {
            throw new KeyNotFoundException(
                "Game attempt not found.");
        }

        var attempt = stored.Attempt;
        var progress = stored.Progress;

        if (attempt.Id != command.AttemptId ||
            attempt.GameId != command.GameId)
        {
            throw new UnauthorizedAccessException(
                "Game attempt does not belong to this request.");
        }

        if (nowUtc < attempt.StartedAtUtc ||
            nowUtc >= stored.DeadlineUtc)
        {
            throw new InvalidOperationException(
                "Game attempt deadline is not active.");
        }

        // Confirm the requested stage exists.
        bool validStep = game.DraftSteps.Any(
            step =>
                step.GameId == command.GameId &&
                step.StepNumber == command.StepNumber);

        if (!validStep)
        {
            throw new InvalidOperationException(
                "Requested Game stage does not exist.");
        }

        // Domain rules enforce:
        // - Published, playable Game
        // - Creator cannot play
        // - Player ownership
        // - Matching attempt and progress
        // - Current stage only
        // - InProgress status only
        GamePlayRules.EnsureCanSubmitEvidence(
            game,
            attempt,
            progress,
            evidence,
            playerUserId,
            nowUtc);

        return progress;
    }
}
