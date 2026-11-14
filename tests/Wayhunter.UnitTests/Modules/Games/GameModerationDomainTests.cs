
using System;
using Wayhunter.Domain.Modules.Games.Entities;
using Wayhunter.Domain.Modules.Games.Enums;
using Xunit;

namespace Wayhunter.UnitTests.Modules.Games;

public sealed class GameModerationDomainTests
{
    private const string CreatorId = "creator-001";

    private static readonly DateTimeOffset SubmittedAt =
        new(2026, 11, 14, 6, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset ApprovedAt =
        SubmittedAt.AddHours(2);

    private static readonly DateTimeOffset ModeratedAt =
        ApprovedAt.AddMinutes(30);

    // =====================================
    // TEST HELPERS
    // =====================================

    private static Game NewDraft()
    {
        var game = Game.CreateDraft(
            Guid.NewGuid(),
            CreatorId,
            "Jaffna Treasure Hunt",
            "Find the hidden treasure",
            60);

        var step = new GameStep(
            Guid.NewGuid(),
            game.Id,
            1);

        var hint = new GameHint(
            Guid.NewGuid(),
            step.Id,
            "Find the first location",
            null);

        game.ReplaceDraftStages(
            CreatorId,
            new[] { step },
            new[] { hint });

        return game;
    }

    private static Game NewSubmittedGame()
    {
        var game = NewDraft();

        game.SubmitForAdminReview(
            CreatorId,
            SubmittedAt);

        return game;
    }

    private static Game NewPublishedGame()
    {
        var game = NewSubmittedGame();

        game.ApproveCreation(ApprovedAt);

        return game;
    }

    // =====================================
    // REJECT TESTS
    // =====================================

    [Fact]
    public void ValidRejection_ShouldStoreReason()
    {
        var game = NewSubmittedGame();

        game.RejectCreation(
            " Invalid game location ",
            ModeratedAt);

        Assert.Equal(GameStatus.Rejected, game.Status);

        Assert.Equal(
            "Invalid game location",
            game.RejectionReason);

        Assert.Equal(
            ModeratedAt,
            game.LastModeratedAtUtc);

        Assert.Null(game.ApprovedAtUtc);
        Assert.False(game.CanPlay(ModeratedAt));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejection_ShouldRequireReason(
        string? reason)
    {
        var game = NewSubmittedGame();

        Assert.Throws<ArgumentException>(
            () => game.RejectCreation(
                reason!,
                ModeratedAt));

        Assert.Equal(
            GameStatus.PendingApproval,
            game.Status);

        Assert.Null(game.RejectionReason);
    }

    [Fact]
    public void DraftGame_CannotBeRejected()
    {
        var game = NewDraft();

        Assert.Throws<InvalidOperationException>(
            () => game.RejectCreation(
                "Invalid game",
                ModeratedAt));

        Assert.Equal(GameStatus.Draft, game.Status);
    }

    [Fact]
    public void EarlyRejection_ShouldBeRejected()
    {
        var game = NewSubmittedGame();

        Assert.Throws<InvalidOperationException>(
            () => game.RejectCreation(
                "Invalid location",
                SubmittedAt.AddMinutes(-1)));

        Assert.Equal(
            GameStatus.PendingApproval,
            game.Status);

        Assert.Null(game.RejectionReason);
    }

    [Fact]
    public void NonUtcRejection_ShouldBeRejected()
    {
        var game = NewSubmittedGame();

        var nonUtc = new DateTimeOffset(
            2026, 11, 14, 15, 30, 0,
            TimeSpan.FromMinutes(330));

        Assert.Throws<ArgumentException>(
            () => game.RejectCreation(
                "Invalid location",
                nonUtc));

        Assert.Equal(
            GameStatus.PendingApproval,
            game.Status);
    }

    [Fact]
    public void DuplicateRejection_ShouldBeRejected()
    {
        var game = NewSubmittedGame();

        game.RejectCreation(
            "Unsafe game",
            ModeratedAt);

        Assert.Throws<InvalidOperationException>(
            () => game.RejectCreation(
                "Another reason",
                ModeratedAt.AddMinutes(1)));

        Assert.Equal("Unsafe game", game.RejectionReason);
    }

    // =====================================
    // FLAG TESTS
    // =====================================

    [Fact]
    public void FlagPendingGame_ShouldBlockApproval()
    {
        var game = NewSubmittedGame();

        game.FlagForReview(
            " Suspicious clues ",
            ModeratedAt);

        Assert.True(game.IsFlaggedForReview);

        Assert.Equal(
            "Suspicious clues",
            game.FlagReason);

        Assert.Equal(
            GameStatus.PendingApproval,
            game.Status);

        Assert.Throws<InvalidOperationException>(
            () => game.ApproveCreation(
                ModeratedAt.AddMinutes(1)));

        Assert.Null(game.ApprovedAtUtc);
    }

    [Fact]
    public void FlagPublishedGame_ShouldBlockPlay()
    {
        var game = NewPublishedGame();

        Assert.True(game.CanPlay(ApprovedAt));

        game.FlagForReview(
            "Suspicious activity",
            ModeratedAt);

        Assert.True(game.IsFlaggedForReview);

        Assert.Equal(
            GameStatus.Published,
            game.Status);

        Assert.False(game.CanPlay(ModeratedAt));

        Assert.Throws<InvalidOperationException>(
            () => game.ApproveVerifiedWinner(
                "player-001",
                ModeratedAt));

        Assert.Null(game.WinnerUserId);
    }

    [Fact]
    public void DuplicateFlag_ShouldBeRejected()
    {
        var game = NewSubmittedGame();

        game.FlagForReview(
            "First report",
            ModeratedAt);

        Assert.Throws<InvalidOperationException>(
            () => game.FlagForReview(
                "Second report",
                ModeratedAt.AddMinutes(1)));

        Assert.Equal(
            "First report",
            game.FlagReason);

        Assert.Equal(
            ModeratedAt,
            game.LastModeratedAtUtc);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Flag_ShouldRequireReason(
        string? reason)
    {
        var game = NewSubmittedGame();

        Assert.Throws<ArgumentException>(
            () => game.FlagForReview(
                reason!,
                ModeratedAt));

        Assert.False(game.IsFlaggedForReview);
        Assert.Null(game.FlagReason);
    }

    [Fact]
    public void RejectedGame_CannotBeFlagged()
    {
        var game = NewSubmittedGame();

        game.RejectCreation(
            "Invalid game",
            ModeratedAt);

        Assert.Throws<InvalidOperationException>(
            () => game.FlagForReview(
                "New report",
                ModeratedAt.AddMinutes(1)));

        Assert.False(game.IsFlaggedForReview);
    }

    [Fact]
    public void EarlyFlag_ShouldBeRejected()
    {
        var game = NewSubmittedGame();

        Assert.Throws<InvalidOperationException>(
            () => game.FlagForReview(
                "Suspicious clues",
                SubmittedAt.AddMinutes(-1)));

        Assert.False(game.IsFlaggedForReview);
    }

    [Fact]
    public void NonUtcFlag_ShouldBeRejected()
    {
        var game = NewSubmittedGame();

        var nonUtc = new DateTimeOffset(
            2026, 11, 14, 15, 30, 0,
            TimeSpan.FromMinutes(330));

        Assert.Throws<ArgumentException>(
            () => game.FlagForReview(
                "Suspicious clues",
                nonUtc));

        Assert.False(game.IsFlaggedForReview);
    }

    // =====================================
    // DISABLE TESTS
    // =====================================

    [Fact]
    public void DisablePublishedGame_ShouldBlockPlay()
    {
        var game = NewPublishedGame();

        Assert.True(game.CanPlay(ApprovedAt));

        game.DisableGame(
            " Unsafe game content ",
            ModeratedAt);

        Assert.Equal(
            GameStatus.Disabled,
            game.Status);

        Assert.Equal(
            "Unsafe game content",
            game.DisabledReason);

        Assert.Equal(
            ModeratedAt,
            game.LastModeratedAtUtc);

        Assert.False(game.CanPlay(ModeratedAt));

        Assert.Throws<InvalidOperationException>(
            () => game.ApproveVerifiedWinner(
                "player-001",
                ModeratedAt));

        Assert.Null(game.WinnerUserId);
    }

    [Fact]
    public void PendingGame_CannotBeDisabled()
    {
        var game = NewSubmittedGame();

        Assert.Throws<InvalidOperationException>(
            () => game.DisableGame(
                "Unsafe game",
                ModeratedAt));

        Assert.Equal(
            GameStatus.PendingApproval,
            game.Status);

        Assert.Null(game.DisabledReason);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Disable_ShouldRequireReason(
        string? reason)
    {
        var game = NewPublishedGame();

        Assert.Throws<ArgumentException>(
            () => game.DisableGame(
                reason!,
                ModeratedAt));

        Assert.Equal(
            GameStatus.Published,
            game.Status);

        Assert.Null(game.DisabledReason);
    }

    [Fact]
    public void DisableBeforeApproval_ShouldBeRejected()
    {
        var game = NewPublishedGame();

        Assert.Throws<InvalidOperationException>(
            () => game.DisableGame(
                "Unsafe game",
                ApprovedAt.AddMinutes(-1)));

        Assert.Equal(
            GameStatus.Published,
            game.Status);

        Assert.Null(game.DisabledReason);
    }

    [Fact]
    public void NonUtcDisable_ShouldBeRejected()
    {
        var game = NewPublishedGame();

        var nonUtc = new DateTimeOffset(
            2026, 11, 14, 15, 30, 0,
            TimeSpan.FromMinutes(330));

        Assert.Throws<ArgumentException>(
            () => game.DisableGame(
                "Unsafe game",
                nonUtc));

        Assert.Equal(
            GameStatus.Published,
            game.Status);
    }

    [Fact]
    public void DuplicateDisable_ShouldBeRejected()
    {
        var game = NewPublishedGame();

        game.DisableGame(
            "First reason",
            ModeratedAt);

        Assert.Throws<InvalidOperationException>(
            () => game.DisableGame(
                "Second reason",
                ModeratedAt.AddMinutes(1)));

        Assert.Equal(
            "First reason",
            game.DisabledReason);
    }

    // =====================================
    // REASON LENGTH VALIDATION
    // =====================================

    [Fact]
    public void ReasonOver1000Characters_ShouldBeRejected()
    {
        var reason = new string('X', 1001);

        var pending = NewSubmittedGame();
        var published = NewPublishedGame();

        Assert.Throws<ArgumentException>(
            () => pending.RejectCreation(
                reason,
                ModeratedAt));

        Assert.Throws<ArgumentException>(
            () => pending.FlagForReview(
                reason,
                ModeratedAt));

        Assert.Throws<ArgumentException>(
            () => published.DisableGame(
                reason,
                ModeratedAt));

        Assert.Equal(
            GameStatus.PendingApproval,
            pending.Status);

        Assert.Equal(
            GameStatus.Published,
            published.Status);
    }
}
