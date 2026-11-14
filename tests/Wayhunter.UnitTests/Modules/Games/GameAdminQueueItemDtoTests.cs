
using System;
using Wayhunter.Application.Modules.Games.Dtos;
using Wayhunter.Domain.Modules.Games.Entities;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Xunit;

namespace Wayhunter.UnitTests.Modules.Games;

public sealed class GameAdminQueueItemDtoTests
{
    private static readonly DateTimeOffset CreatedAt =
        new(2026, 11, 14, 5, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset SubmittedAt =
        CreatedAt.AddHours(2);

    // =====================================
    // CREATE ACTIVE USER
    // =====================================

    private static UserAccount NewActiveUser(
        string name = "Thuvarathan")
    {
        var user = UserAccount.RegisterPending(
            name,
            "creator@example.com",
            "test-password-hash",
            CreatedAt);

        user.ActivateAfterVerifiedOtp(
            CreatedAt.AddMinutes(10));

        return user;
    }

    // =====================================
    // CREATE VALID GAME DRAFT
    // =====================================

    private static Game NewDraft(string creatorId)
    {
        var game = Game.CreateDraft(
            Guid.NewGuid(),
            creatorId,
            "Jaffna Treasure Hunt",
            "Find hidden places",
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
            creatorId,
            new[] { step },
            new[] { hint });

        return game;
    }

    private static Game NewSubmittedGame(
        UserAccount creator)
    {
        var game = NewDraft(creator.UserId);

        game.SubmitForAdminReview(
            creator.UserId,
            SubmittedAt);

        return game;
    }

    // TEST 01 - VALID ADMIN QUEUE ITEM

    [Fact]
    public void SubmittedGame_ShouldCreateQueueItem()
    {
        var creator = NewActiveUser();
        var game = NewSubmittedGame(creator);

        var item = GameAdminQueueItemDto
            .FromSubmittedGame(game, creator);

        Assert.Equal(game.Id, item.GameId);

        Assert.Equal(
            creator.UserId,
            item.CreatorUserId);

        Assert.Equal(
            "Thuvarathan",
            item.CreatorNameSnapshot);

        Assert.Equal(
            "Jaffna Treasure Hunt",
            item.GameTitle);

        Assert.Equal(
            SubmittedAt,
            item.SubmittedAtUtc);
    }

    // TEST 02 - DRAFT MUST NOT APPEAR

    [Fact]
    public void DraftGame_ShouldNotCreateQueueItem()
    {
        var creator = NewActiveUser();
        var game = NewDraft(creator.UserId);

        Assert.Throws<InvalidOperationException>(() =>
            GameAdminQueueItemDto.FromSubmittedGame(
                game,
                creator));
    }

    // TEST 03 - LEGACY PENDING GAME

    [Fact]
    public void LegacyPendingGame_ShouldBeRejected()
    {
        var creator = NewActiveUser();

        var game = new Game(
            Guid.NewGuid(),
            creator.UserId);

        Assert.Throws<InvalidOperationException>(() =>
            GameAdminQueueItemDto.FromSubmittedGame(
                game,
                creator));
    }

    // TEST 04 - WRONG CREATOR IDENTITY

    [Fact]
    public void DifferentUser_ShouldBeRejected()
    {
        var creator = NewActiveUser();
        var game = NewSubmittedGame(creator);

        var anotherUser = NewActiveUser(
            "Another Player");

        Assert.Throws<UnauthorizedAccessException>(() =>
            GameAdminQueueItemDto.FromSubmittedGame(
                game,
                anotherUser));
    }

    // TEST 05 - UNVERIFIED USER

    [Fact]
    public void UnverifiedCreator_ShouldBeRejected()
    {
        var creator = UserAccount.RegisterPending(
            "Pending Player",
            "pending@example.com",
            "test-password-hash",
            CreatedAt);

        var game = NewSubmittedGame(creator);

        Assert.Throws<InvalidOperationException>(() =>
            GameAdminQueueItemDto.FromSubmittedGame(
                game,
                creator));
    }

    // TEST 06 - SUSPENDED USER

    [Fact]
    public void SuspendedCreator_ShouldBeRejected()
    {
        var creator = NewActiveUser();
        var game = NewSubmittedGame(creator);

        creator.Suspend();

        Assert.Throws<InvalidOperationException>(() =>
            GameAdminQueueItemDto.FromSubmittedGame(
                game,
                creator));
    }

    // TEST 07 - APPROVED GAME NOT PENDING

    [Fact]
    public void PublishedGame_ShouldNotCreateQueueItem()
    {
        var creator = NewActiveUser();
        var game = NewSubmittedGame(creator);

        game.ApproveCreation(
            SubmittedAt.AddMinutes(30));

        Assert.Throws<InvalidOperationException>(() =>
            GameAdminQueueItemDto.FromSubmittedGame(
                game,
                creator));
    }

    // TEST 08 - REJECTED GAME NOT PENDING

    [Fact]
    public void RejectedGame_ShouldNotCreateQueueItem()
    {
        var creator = NewActiveUser();
        var game = NewSubmittedGame(creator);

        game.RejectCreation();

        Assert.Throws<InvalidOperationException>(() =>
            GameAdminQueueItemDto.FromSubmittedGame(
                game,
                creator));
    }

    // TEST 09 - NULL GAME

    [Fact]
    public void NullGame_ShouldBeRejected()
    {
        var creator = NewActiveUser();

        Assert.Throws<ArgumentNullException>(() =>
            GameAdminQueueItemDto.FromSubmittedGame(
                null!,
                creator));
    }

    // TEST 10 - NULL CREATOR

    [Fact]
    public void NullCreator_ShouldBeRejected()
    {
        var creator = NewActiveUser();
        var game = NewSubmittedGame(creator);

        Assert.Throws<ArgumentNullException>(() =>
            GameAdminQueueItemDto.FromSubmittedGame(
                game,
                null!));
    }
}
