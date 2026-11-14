
using System;
using Wayhunter.Domain.Modules.Games.Entities;
using Wayhunter.Domain.Modules.Games.Enums;
using Xunit;

namespace Wayhunter.UnitTests.Modules.Games;

public sealed class GameSubmissionDomainTests
{
    private const string CreatorId = "creator-001";

    private static readonly DateTimeOffset SubmittedAt =
        new(2026, 11, 14, 6, 0, 0, TimeSpan.Zero);

    // =====================================
    // CREATE A VALID GAME DRAFT
    // =====================================

    private static Game NewValidDraft()
    {
        var game = Game.CreateDraft(
            Guid.NewGuid(),
            CreatorId,
            "Jaffna Treasure Hunt",
            "Find hidden locations",
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

    // TEST 01 - VALID SUBMISSION

    [Fact]
    public void ValidDraft_ShouldBecomePendingApproval()
    {
        var game = NewValidDraft();

        game.SubmitForAdminReview(
            CreatorId,
            SubmittedAt);

        Assert.Equal(
            GameStatus.PendingApproval,
            game.Status);

        Assert.Equal(
            SubmittedAt,
            game.SubmittedAtUtc);

        Assert.Null(game.ApprovedAtUtc);
        Assert.Null(game.ExpiresAtUtc);
    }

    // TEST 02 - CREATOR ONLY

    [Fact]
    public void AnotherUser_ShouldNotSubmitDraft()
    {
        var game = NewValidDraft();

        Assert.Throws<UnauthorizedAccessException>(
            () => game.SubmitForAdminReview(
                "another-user",
                SubmittedAt));

        Assert.Equal(GameStatus.Draft, game.Status);
        Assert.Null(game.SubmittedAtUtc);
    }

    // TEST 03 - NO DUPLICATE SUBMISSION

    [Fact]
    public void DuplicateSubmission_ShouldBeRejected()
    {
        var game = NewValidDraft();

        game.SubmitForAdminReview(
            CreatorId,
            SubmittedAt);

        Assert.Throws<InvalidOperationException>(
            () => game.SubmitForAdminReview(
                CreatorId,
                SubmittedAt.AddMinutes(1)));

        Assert.Equal(
            SubmittedAt,
            game.SubmittedAtUtc);
    }

    // TEST 04 - STAGES ARE REQUIRED

    [Fact]
    public void DraftWithoutStages_ShouldNotSubmit()
    {
        var game = Game.CreateDraft(
            Guid.NewGuid(),
            CreatorId,
            "Incomplete Game",
            null,
            60);

        Assert.Throws<InvalidOperationException>(
            () => game.SubmitForAdminReview(
                CreatorId,
                SubmittedAt));

        Assert.Equal(GameStatus.Draft, game.Status);
        Assert.Null(game.SubmittedAtUtc);
    }

    // TEST 05 - UTC REQUIRED

    [Fact]
    public void NonUtcTime_ShouldBeRejected()
    {
        var game = NewValidDraft();

        var nonUtcTime = new DateTimeOffset(
            2026, 11, 14, 12, 0, 0,
            TimeSpan.FromMinutes(330));

        Assert.Throws<ArgumentException>(
            () => game.SubmitForAdminReview(
                CreatorId,
                nonUtcTime));

        Assert.Equal(GameStatus.Draft, game.Status);
    }

    // TEST 06 - TIMESTAMP REQUIRED

    [Fact]
    public void MissingTimestamp_ShouldBeRejected()
    {
        var game = NewValidDraft();

        Assert.Throws<ArgumentException>(
            () => game.SubmitForAdminReview(
                CreatorId,
                default));

        Assert.Null(game.SubmittedAtUtc);
        Assert.Equal(GameStatus.Draft, game.Status);
    }

    // TEST 07 - NOT PUBLIC BEFORE APPROVAL

    [Fact]
    public void SubmittedGame_ShouldNotBePlayable()
    {
        var game = NewValidDraft();

        game.SubmitForAdminReview(
            CreatorId,
            SubmittedAt);

        Assert.False(
            game.CanPlay(SubmittedAt.AddMinutes(1)));

        Assert.Equal(
            GameStatus.PendingApproval,
            game.Status);
    }

    // TEST 08 - CANNOT EDIT AFTER SUBMISSION

    [Fact]
    public void SubmittedGame_ShouldNotBeEditable()
    {
        var game = NewValidDraft();

        game.SubmitForAdminReview(
            CreatorId,
            SubmittedAt);

        Assert.Throws<InvalidOperationException>(
            () => game.UpdateDraft(
                CreatorId,
                "Changed Title",
                "Changed Description",
                90));

        Assert.Equal(
            "Jaffna Treasure Hunt",
            game.Title);
    }

    // TEST 09 - LEGACY PENDING IS NOT A DRAFT

    [Fact]
    public void LegacyPendingGame_ShouldNotSubmitAgain()
    {
        var game = new Game(
            Guid.NewGuid(),
            CreatorId);

        Assert.Throws<InvalidOperationException>(
            () => game.SubmitForAdminReview(
                CreatorId,
                SubmittedAt));

        Assert.Null(game.SubmittedAtUtc);
    }

    // TEST 10 - EXPIRY STARTS AT ADMIN APPROVAL

    [Fact]
    public void AdminApproval_ShouldStart30DayExpiry()
    {
        var game = NewValidDraft();

        game.SubmitForAdminReview(
            CreatorId,
            SubmittedAt);

        var approvedAt = SubmittedAt.AddHours(2);

        game.ApproveCreation(approvedAt);

        Assert.Equal(
            GameStatus.Published,
            game.Status);

        Assert.Equal(
            approvedAt,
            game.ApprovedAtUtc);

        Assert.Equal(
            approvedAt.AddDays(30),
            game.ExpiresAtUtc);

        Assert.Equal(
            SubmittedAt,
            game.SubmittedAtUtc);
    }
}
