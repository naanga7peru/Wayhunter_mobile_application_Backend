
using System;
using Wayhunter.Domain.Modules.Games.Enums;

namespace Wayhunter.Infrastructure.Persistence.Models.Games;

// M5 SQL persistence models.
// These do not replace existing Game domain entities.

public sealed class GameRow
{
    public Guid Id { get; set; }
    public string CreatorUserId { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? Description { get; set; }
    public int? DurationMinutes { get; set; }

    public GameStatus Status { get; set; } = GameStatus.Draft;

    public DateTimeOffset? SubmittedAtUtc { get; set; }
    public DateTimeOffset? ApprovedAtUtc { get; set; }
    public DateTimeOffset? ExpiresAtUtc { get; set; }

    public bool IsFlaggedForReview { get; set; }
    public string? FlagReason { get; set; }
    public string? RejectionReason { get; set; }
    public string? DisabledReason { get; set; }
    public DateTimeOffset? LastModeratedAtUtc { get; set; }

    public byte[] RowVersion { get; set; } = [];
}

public sealed class GameStepRow
{
    public Guid Id { get; set; }
    public Guid GameId { get; set; }
    public int StepNumber { get; set; }
}

public sealed class GameHintRow
{
    public Guid Id { get; set; }
    public Guid GameStepId { get; set; }
    public string? TextHint { get; set; }
    public string? PhotoMediaId { get; set; }
}

public sealed class GameAttemptRow
{
    public Guid Id { get; set; }
    public Guid GameId { get; set; }
    public string PlayerUserId { get; set; } = string.Empty;
    public DateTimeOffset StartedAtUtc { get; set; }
    public DateTimeOffset? DeadlineUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

public sealed class GameAttemptProgressRow
{
    public Guid AttemptId { get; set; }
    public Guid GameId { get; set; }
    public string PlayerUserId { get; set; } = string.Empty;
    public int TotalSteps { get; set; }
    public int CurrentStepNumber { get; set; } = 1;
    public int RejectedEvidenceCount { get; set; }

    public GameProgressStatus Status { get; set; }
        = GameProgressStatus.NotStarted;
}

public sealed class GameProofEvidenceRow
{
    public Guid Id { get; set; }
    public Guid AttemptId { get; set; }
    public Guid GameId { get; set; }
    public int StepNumber { get; set; }
    public string MediaId { get; set; } = string.Empty;
    public DateTimeOffset SubmittedAtUtc { get; set; }
}

public sealed class GameCompletionResultRow
{
    public Guid Id { get; set; }
    public Guid GameId { get; set; }
    public Guid AttemptId { get; set; }
    public string PlayerUserId { get; set; } = string.Empty;
    public DateTimeOffset AttemptStartedAtUtc { get; set; }
    public DateTimeOffset AttemptDeadlineUtc { get; set; }
    public DateTimeOffset CompletedAtUtc { get; set; }

    public GameCompletionResultStatus Status { get; set; }
        = GameCompletionResultStatus.PendingAdmin;

    public byte[] RowVersion { get; set; } = [];
}

public sealed class GameCompletionEvidenceRow
{
    public Guid CompletionResultId { get; set; }
    public Guid EvidenceId { get; set; }
    public Guid AttemptId { get; set; }
}

public sealed class GameWinnerRow
{
    // One winner per Game.
    public Guid GameId { get; set; }
    public Guid AttemptId { get; set; }

    public string PlayerUserId { get; set; } = string.Empty;
    public DateTimeOffset ApprovedAtUtc { get; set; }
    public string ApprovedByUserId { get; set; } = string.Empty;
}
