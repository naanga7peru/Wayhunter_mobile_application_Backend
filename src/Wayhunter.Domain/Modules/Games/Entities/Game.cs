
using System;
using System.Collections.Generic;
using Wayhunter.Domain.Modules.Games.Enums;

namespace Wayhunter.Domain.Modules.Games.Entities;

public sealed class Game
{
    // =====================================
    // GAME BASIC DETAILS
    // =====================================

    public Guid Id { get; }

    public string CreatorUserId { get; }

    public string? Title { get; private set; }

    public string? Description { get; private set; }

    public int? DurationMinutes { get; private set; }

    public GameStatus Status { get; private set; }
        = GameStatus.PendingApproval;

    // =====================================
    // SUBMISSION, APPROVAL AND EXPIRY
    // =====================================

    public DateTimeOffset? SubmittedAtUtc
    { get; private set; }

    public DateTimeOffset? ApprovedAtUtc
    { get; private set; }

    public DateTimeOffset? ExpiresAtUtc
    { get; private set; }

    public string? WinnerUserId
    { get; private set; }

    // =====================================
    // M5-MS03-T05 - MODERATION PROPERTIES
    // =====================================

    public bool IsFlaggedForReview
    { get; private set; }

    public string? FlagReason
    { get; private set; }

    public string? RejectionReason
    { get; private set; }

    public string? DisabledReason
    { get; private set; }

    public DateTimeOffset? LastModeratedAtUtc
    { get; private set; }

    // =====================================
    // GAME DRAFT STAGES AND HINTS
    // =====================================

    private readonly List<GameStep> _draftSteps = new();

    private readonly List<GameHint> _draftHints = new();

    public IReadOnlyList<GameStep> DraftSteps =>
        _draftSteps.AsReadOnly();

    public IReadOnlyList<GameHint> DraftHints =>
        _draftHints.AsReadOnly();

    // =====================================
    // CONSTRUCTOR
    // =====================================

    public Game(Guid id, string creatorUserId)
    {
        if (id == Guid.Empty)
            throw new ArgumentException(
                "Game ID required.");

        if (string.IsNullOrWhiteSpace(creatorUserId))
            throw new ArgumentException(
                "Creator ID required.");

        Id = id;
        CreatorUserId = creatorUserId;
    }

    // =====================================
    // CREATE GAME DRAFT
    // =====================================

    public static Game CreateDraft(
        Guid id,
        string creatorUserId,
        string title,
        string? description,
        int durationMinutes)
    {
        var game = new Game(
            id,
            creatorUserId);

        game.Status = GameStatus.Draft;

        game.UpdateDraft(
            creatorUserId,
            title,
            description,
            durationMinutes);

        return game;
    }

    // =====================================
    // UPDATE GAME DRAFT
    // =====================================

    public void UpdateDraft(
        string actorUserId,
        string title,
        string? description,
        int durationMinutes)
    {
        if (Status != GameStatus.Draft)
            throw new InvalidOperationException(
                "Only draft games can be edited.");

        if (!string.Equals(
            actorUserId,
            CreatorUserId,
            StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException(
                "Only the creator can edit this game.");
        }

        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException(
                "Game title is required.");

        var cleanTitle = title.Trim();

        if (cleanTitle.Length > 150)
            throw new ArgumentException(
                "Game title cannot exceed 150 characters.");

        var cleanDescription = description?.Trim();

        if (cleanDescription?.Length > 2000)
            throw new ArgumentException(
                "Game description cannot exceed 2000 characters.");

        if (durationMinutes <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(durationMinutes),
                "Game duration must be positive.");

        Title = cleanTitle;
        Description = cleanDescription;
        DurationMinutes = durationMinutes;
    }

    // =====================================
    // REPLACE GAME DRAFT STAGES
    // =====================================

    public void ReplaceDraftStages(
        string actorUserId,
        IReadOnlyList<GameStep> steps,
        IReadOnlyList<GameHint> hints)
    {
        if (Status != GameStatus.Draft)
            throw new InvalidOperationException(
                "Only draft games can change stages.");

        if (!string.Equals(
            actorUserId,
            CreatorUserId,
            StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException(
                "Only the creator can change stages.");
        }

        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(hints);

        if (steps.Count == 0)
            throw new ArgumentException(
                "At least one stage is required.");

        var stepIds = new HashSet<Guid>();
        var stepNumbers = new HashSet<int>();
        var hintStepIds = new HashSet<Guid>();

        foreach (var step in steps)
        {
            if (step is null || step.GameId != Id)
                throw new ArgumentException(
                    "Stage belongs to another game.");

            if (!stepIds.Add(step.Id) ||
                !stepNumbers.Add(step.StepNumber))
            {
                throw new ArgumentException(
                    "Duplicate stage found.");
            }
        }

        for (int number = 1;
             number <= steps.Count;
             number++)
        {
            if (!stepNumbers.Contains(number))
                throw new ArgumentException(
                    "Stage numbers must start at 1 without gaps.");
        }

        foreach (var hint in hints)
        {
            if (hint is null ||
                !stepIds.Contains(hint.GameStepId))
            {
                throw new ArgumentException(
                    "Hint belongs to an invalid stage.");
            }

            hintStepIds.Add(hint.GameStepId);
        }

        foreach (var step in steps)
        {
            if (!hintStepIds.Contains(step.Id))
                throw new ArgumentException(
                    "Every stage must have a hint.");
        }

        _draftSteps.Clear();
        _draftSteps.AddRange(steps);

        _draftHints.Clear();
        _draftHints.AddRange(hints);
    }

    // =====================================
    // SUBMIT DRAFT FOR ADMIN REVIEW
    // M5-MS03-T01
    // =====================================

    public void SubmitForAdminReview(
        string actorUserId,
        DateTimeOffset submittedAtUtc)
    {
        if (Status != GameStatus.Draft)
        {
            throw new InvalidOperationException(
                "Only draft games can be submitted.");
        }

        if (!string.Equals(
            actorUserId,
            CreatorUserId,
            StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException(
                "Only the creator can submit this game.");
        }

        if (submittedAtUtc == default)
        {
            throw new ArgumentException(
                "Submission timestamp is required.");
        }

        RequireUtc(submittedAtUtc);

        if (string.IsNullOrWhiteSpace(Title))
        {
            throw new InvalidOperationException(
                "Game title is required before submission.");
        }

        if (DurationMinutes is null or <= 0)
        {
            throw new InvalidOperationException(
                "Game duration is required before submission.");
        }

        if (_draftSteps.Count == 0)
        {
            throw new InvalidOperationException(
                "At least one game stage is required.");
        }

        foreach (var step in _draftSteps)
        {
            if (!_draftHints.Exists(
                hint => hint.GameStepId == step.Id))
            {
                throw new InvalidOperationException(
                    "Every stage requires a clue.");
            }
        }

        SubmittedAtUtc = submittedAtUtc;
        Status = GameStatus.PendingApproval;
    }

    // =====================================
    // ADMIN APPROVAL
    // M5-MS03-T04
    // =====================================

    public void ApproveCreation(
        DateTimeOffset approvedAtUtc)
    {
        if (Status != GameStatus.PendingApproval)
            throw new InvalidOperationException(
                "Only pending games can be approved.");

        // M5-MS03-T05: Block flagged games.
        if (IsFlaggedForReview)
        {
            throw new InvalidOperationException(
                "Flagged game requires Admin review clearance.");
        }

        RequireUtc(approvedAtUtc);

        ApprovedAtUtc = approvedAtUtc;

        // Exactly 30 days after approval.
        ExpiresAtUtc = approvedAtUtc.AddHours(30 * 24);

        Status = GameStatus.Published;
    }

    // =====================================
    // LEGACY ADMIN REJECTION
    // PRESERVED FOR EXISTING CALLERS
    // =====================================

    public void RejectCreation()
    {
        if (Status != GameStatus.PendingApproval)
            throw new InvalidOperationException(
                "Only pending games can be rejected.");

        Status = GameStatus.Rejected;
    }

    // =====================================
    // M5-MS03-T05 - REJECT WITH REASON
    // =====================================

    public void RejectCreation(
        string reason,
        DateTimeOffset rejectedAtUtc)
    {
        if (Status != GameStatus.PendingApproval ||
            !SubmittedAtUtc.HasValue)
        {
            throw new InvalidOperationException(
                "Only submitted pending games can be rejected.");
        }

        string cleanReason =
            ValidateModerationReason(reason);

        RequireUtc(rejectedAtUtc);

        if (rejectedAtUtc < SubmittedAtUtc.Value)
        {
            throw new InvalidOperationException(
                "Rejection cannot precede submission.");
        }

        RejectionReason = cleanReason;
        LastModeratedAtUtc = rejectedAtUtc;
        Status = GameStatus.Rejected;
    }

    // =====================================
    // M5-MS03-T05 - FLAG SUSPICIOUS GAME
    // =====================================

    public void FlagForReview(
        string reason,
        DateTimeOffset flaggedAtUtc)
    {
        if (Status != GameStatus.PendingApproval &&
            Status != GameStatus.Published)
        {
            throw new InvalidOperationException(
                "Only pending or published games can be flagged.");
        }

        if (IsFlaggedForReview)
        {
            throw new InvalidOperationException(
                "Game is already flagged for review.");
        }

        string cleanReason =
            ValidateModerationReason(reason);

        RequireUtc(flaggedAtUtc);

        if (SubmittedAtUtc.HasValue &&
            flaggedAtUtc < SubmittedAtUtc.Value)
        {
            throw new InvalidOperationException(
                "Flag timestamp cannot precede submission.");
        }

        FlagReason = cleanReason;
        IsFlaggedForReview = true;
        LastModeratedAtUtc = flaggedAtUtc;
    }

    // =====================================
    // M5-MS03-T05 - DISABLE PUBLISHED GAME
    // =====================================

    public void DisableGame(
        string reason,
        DateTimeOffset disabledAtUtc)
    {
        if (Status != GameStatus.Published)
        {
            throw new InvalidOperationException(
                "Only published games can be disabled.");
        }

        string cleanReason =
            ValidateModerationReason(reason);

        RequireUtc(disabledAtUtc);

        if (!ApprovedAtUtc.HasValue ||
            disabledAtUtc < ApprovedAtUtc.Value)
        {
            throw new InvalidOperationException(
                "Disable timestamp cannot precede approval.");
        }

        DisabledReason = cleanReason;
        LastModeratedAtUtc = disabledAtUtc;
        Status = GameStatus.Disabled;
    }

    // =====================================
    // VALIDATE MODERATION REASON
    // =====================================

    private static string ValidateModerationReason(
        string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException(
                "Moderation reason is required.",
                nameof(reason));
        }

        string cleanReason = reason.Trim();

        if (cleanReason.Length > 1000)
        {
            throw new ArgumentException(
                "Moderation reason cannot exceed 1000 characters.",
                nameof(reason));
        }

        return cleanReason;
    }

    // =====================================
    // GAME PLAY ELIGIBILITY
    // =====================================

    public bool CanPlay(DateTimeOffset nowUtc)
    {
        RequireUtc(nowUtc);

        return Status == GameStatus.Published
            && !IsFlaggedForReview
            && WinnerUserId == null
            && ExpiresAtUtc.HasValue
            && nowUtc < ExpiresAtUtc.Value;
    }

    // =====================================
    // APPROVE VERIFIED WINNER
    // =====================================

    public void ApproveVerifiedWinner(
        string playerUserId,
        DateTimeOffset nowUtc)
    {
        if (string.IsNullOrWhiteSpace(playerUserId))
            throw new ArgumentException(
                "Player ID required.");

        if (playerUserId == CreatorUserId)
            throw new InvalidOperationException(
                "Creator cannot win their own game.");

        if (!CanPlay(nowUtc))
            throw new InvalidOperationException(
                "Game expired, disabled, flagged, closed or already won.");

        WinnerUserId = playerUserId;
    }

    // =====================================
    // ARCHIVE EXPIRED GAME
    // =====================================

    public bool ArchiveIfExpired(
        DateTimeOffset nowUtc)
    {
        RequireUtc(nowUtc);

        if (Status != GameStatus.Published ||
            !ExpiresAtUtc.HasValue ||
            nowUtc < ExpiresAtUtc.Value)
        {
            return false;
        }

        Status = GameStatus.Expired;

        return true;
    }

    // =====================================
    // UTC TIMESTAMP VALIDATION
    // =====================================

    private static void RequireUtc(
        DateTimeOffset value)
    {
        if (value.Offset != TimeSpan.Zero)
            throw new ArgumentException(
                "UTC timestamp required.");
    }
}
