
using System;
using Wayhunter.Domain.Modules.Games.Enums;

namespace Wayhunter.Infrastructure.Persistence.Models.Games;

/// <summary>
/// Durable game approval audit record.
/// One creator approval is permitted per Game.
/// M6 owns the actual Coins reward outbox.
/// </summary>
public sealed class GameCreationApprovalRow
{
    public string ApprovalRecordId { get; set; } = string.Empty;

    public Guid GameId { get; set; }

    public string AdminUserId { get; set; } = string.Empty;

    public DateTimeOffset ApprovedAtUtc { get; set; }

    public Guid RewardEventId { get; set; }

    public Guid CorrelationId { get; set; }

    public string RewardRuleVersion { get; set; } = string.Empty;
}

/// <summary>
/// Historical Admin moderation decision.
/// </summary>
public sealed class GameModerationDecisionRow
{
    public Guid DecisionId { get; set; }

    public Guid GameId { get; set; }

    public string AdminUserId { get; set; } = string.Empty;

    public GameModerationAction Action { get; set; }

    public string Reason { get; set; } = string.Empty;

    public DateTimeOffset OccurredAtUtc { get; set; }

    public GameStatus ResultingStatus { get; set; }

    public Guid CorrelationId { get; set; }
}

/// <summary>
/// One review decision per submitted proof evidence.
/// Retakes create separate evidence records.
/// </summary>
public sealed class GameProofReviewDecisionRow
{
    public Guid DecisionId { get; set; }

    public Guid EvidenceId { get; set; }

    public string ReviewerUserId { get; set; } = string.Empty;

    public bool IsApproved { get; set; }

    public string? Reason { get; set; }

    public DateTimeOffset ReviewedAtUtc { get; set; }

    public Guid CorrelationId { get; set; }
}

/// <summary>
/// Game completion / winner verification decision.
/// Only one approved result per Game is permitted.
/// </summary>
public sealed class GameResultDecisionRow
{
    public Guid DecisionId { get; set; }

    public Guid GameId { get; set; }

    public Guid AttemptId { get; set; }

    public string AdminUserId { get; set; } = string.Empty;

    public bool IsApproved { get; set; }

    public string? Reason { get; set; }

    public DateTimeOffset OccurredAtUtc { get; set; }

    public Guid CorrelationId { get; set; }
}
