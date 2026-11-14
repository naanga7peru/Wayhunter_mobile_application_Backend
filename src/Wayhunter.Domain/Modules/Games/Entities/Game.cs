
using System;
using System.Collections.Generic;
using Wayhunter.Domain.Modules.Games.Enums;

namespace Wayhunter.Domain.Modules.Games.Entities;

public sealed class Game
{
    // ============================
    // GAME BASIC DETAILS
    // ============================

    public Guid Id { get; }

    public string CreatorUserId { get; }

    public string? Title { get; private set; }

    public string? Description { get; private set; }

    public int? DurationMinutes { get; private set; }

    public GameStatus Status { get; private set; }
        = GameStatus.PendingApproval;

    // ============================
    // APPROVAL AND EXPIRY
    // ============================

    public DateTimeOffset? ApprovedAtUtc { get; private set; }

    public DateTimeOffset? ExpiresAtUtc { get; private set; }

    public string? WinnerUserId { get; private set; }

    // ============================
    // GAME DRAFT STAGES AND HINTS
    // ============================

    private readonly List<GameStep> _draftSteps = new();

    private readonly List<GameHint> _draftHints = new();

    public IReadOnlyList<GameStep> DraftSteps =>
        _draftSteps.AsReadOnly();

    public IReadOnlyList<GameHint> DraftHints =>
        _draftHints.AsReadOnly();

    // ============================
    // EXISTING CONSTRUCTOR
    // ============================

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

    // ============================
    // CREATE GAME DRAFT
    // ============================

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

    // ============================
    // UPDATE GAME DRAFT
    // ============================

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

    // ============================
    // REPLACE GAME DRAFT STAGES
    // ============================

    public void ReplaceDraftStages(
        string actorUserId,
        IReadOnlyList<GameStep> steps,
        IReadOnlyList<GameHint> hints)
    {
        // Only drafts can be edited
        if (Status != GameStatus.Draft)
            throw new InvalidOperationException(
                "Only draft games can change stages.");

        // Only the creator can edit stages
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

        // At least one stage required
        if (steps.Count == 0)
            throw new ArgumentException(
                "At least one stage is required.");

        var stepIds = new HashSet<Guid>();
        var stepNumbers = new HashSet<int>();
        var hintStepIds = new HashSet<Guid>();

        // Validate each stage
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

        // Validate stage order: 1, 2, 3...
        for (int number = 1;
             number <= steps.Count;
             number++)
        {
            if (!stepNumbers.Contains(number))
                throw new ArgumentException(
                    "Stage numbers must start at 1 without gaps.");
        }

        // Validate each hint belongs to a stage
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

        // Every stage requires at least one hint
        foreach (var step in steps)
        {
            if (!hintStepIds.Contains(step.Id))
                throw new ArgumentException(
                    "Every stage must have a hint.");
        }

        // Replace only after all validation succeeds
        _draftSteps.Clear();
        _draftSteps.AddRange(steps);

        _draftHints.Clear();
        _draftHints.AddRange(hints);
    }

    // ============================
    // ADMIN GAME APPROVAL
    // ============================

    public void ApproveCreation(
        DateTimeOffset approvedAtUtc)
    {
        if (Status != GameStatus.PendingApproval)
            throw new InvalidOperationException(
                "Only pending games can be approved.");

        RequireUtc(approvedAtUtc);

        ApprovedAtUtc = approvedAtUtc;

        // Exactly 30 days after Admin approval
        ExpiresAtUtc = approvedAtUtc.AddHours(30 * 24);

        Status = GameStatus.Published;
    }

    // ============================
    // ADMIN GAME REJECTION
    // ============================

    public void RejectCreation()
    {
        if (Status != GameStatus.PendingApproval)
            throw new InvalidOperationException(
                "Only pending games can be rejected.");

        Status = GameStatus.Rejected;
    }

    // ============================
    // GAME PLAY ELIGIBILITY
    // ============================

    public bool CanPlay(DateTimeOffset nowUtc)
    {
        RequireUtc(nowUtc);

        return Status == GameStatus.Published
            && WinnerUserId == null
            && ExpiresAtUtc.HasValue
            && nowUtc < ExpiresAtUtc.Value;
    }

    // ============================
    // APPROVE VERIFIED WINNER
    // ============================

    // Call only after Admin verifies the winning attempt
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
                "Game expired, closed or already won.");

        WinnerUserId = playerUserId;
    }

    // ============================
    // ARCHIVE EXPIRED GAME
    // ============================

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

        // Retain Game record for history
        Status = GameStatus.Expired;

        return true;
    }

    // ============================
    // UTC TIME VALIDATION
    // ============================

    private static void RequireUtc(
        DateTimeOffset value)
    {
        if (value.Offset != TimeSpan.Zero)
            throw new ArgumentException(
                "UTC timestamp required.");
    }
}
