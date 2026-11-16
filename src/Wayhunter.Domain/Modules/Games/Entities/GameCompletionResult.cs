
using System;
using System.Collections.Generic;
using System.Linq;
using Wayhunter.Domain.Modules.Games.Enums;

namespace Wayhunter.Domain.Modules.Games.Entities;

/// <summary>
/// M5-MS07-T01 — Final Game completion result.
///
/// Created only after all required stage proofs
/// have been approved within the attempt deadline.
///
/// PendingAdmin does not authorize Coins.
/// In-memory prototype; SQL migration remains on hold.
/// </summary>
public sealed class GameCompletionResult
{
    public Guid Id { get; }

    public Guid GameId { get; }

    public Guid AttemptId { get; }

    public string PlayerUserId { get; }

    public IReadOnlyList<Guid> ApprovedStageEvidenceIds { get; }

    public DateTimeOffset AttemptStartedAtUtc { get; }

    public DateTimeOffset AttemptDeadlineUtc { get; }

    public DateTimeOffset CompletedAtUtc { get; }

    public GameCompletionResultStatus Status { get; }

    public GameCompletionResult(
        Guid id,
        Guid gameId,
        Guid attemptId,
        string playerUserId,
        IReadOnlyList<Guid> approvedStageEvidenceIds,
        DateTimeOffset attemptStartedAtUtc,
        DateTimeOffset attemptDeadlineUtc,
        DateTimeOffset completedAtUtc)
    {
        if (id == Guid.Empty ||
            gameId == Guid.Empty ||
            attemptId == Guid.Empty)
        {
            throw new ArgumentException(
                "Valid completion, Game and Attempt IDs are required.");
        }

        if (string.IsNullOrWhiteSpace(playerUserId))
        {
            throw new ArgumentException(
                "Player UserId is required.",
                nameof(playerUserId));
        }

        ArgumentNullException.ThrowIfNull(
            approvedStageEvidenceIds);

        if (approvedStageEvidenceIds.Count == 0 ||
            approvedStageEvidenceIds.Any(
                evidenceId => evidenceId == Guid.Empty) ||
            approvedStageEvidenceIds.Distinct().Count() !=
                approvedStageEvidenceIds.Count)
        {
            throw new ArgumentException(
                "Unique approved stage evidence references are required.",
                nameof(approvedStageEvidenceIds));
        }

        if (attemptStartedAtUtc == default ||
            attemptDeadlineUtc == default ||
            completedAtUtc == default ||
            attemptStartedAtUtc.Offset != TimeSpan.Zero ||
            attemptDeadlineUtc.Offset != TimeSpan.Zero ||
            completedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "Valid UTC completion timestamps are required.");
        }

        if (attemptDeadlineUtc <= attemptStartedAtUtc)
        {
            throw new InvalidOperationException(
                "Attempt deadline must be after attempt start.");
        }

        if (completedAtUtc < attemptStartedAtUtc ||
            completedAtUtc >= attemptDeadlineUtc)
        {
            throw new InvalidOperationException(
                "Game completion must occur within the attempt deadline.");
        }

        Id = id;
        GameId = gameId;
        AttemptId = attemptId;
        PlayerUserId = playerUserId.Trim();

        ApprovedStageEvidenceIds =
            Array.AsReadOnly(
                approvedStageEvidenceIds.ToArray());

        AttemptStartedAtUtc = attemptStartedAtUtc;
        AttemptDeadlineUtc = attemptDeadlineUtc;
        CompletedAtUtc = completedAtUtc;

        Status = GameCompletionResultStatus.PendingAdmin;
    }
}
