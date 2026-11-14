
using System;
using Wayhunter.Domain.Modules.Games.Entities;
using Wayhunter.Domain.Modules.Games.Enums;
using Xunit;

namespace Wayhunter.UnitTests.Modules.Games;

public sealed class GameDraftDomainTests
{
    private static Game NewDraft()
    {
        return Game.CreateDraft(
            Guid.NewGuid(),
            "creator-1",
            "Treasure Hunt",
            "Find the hidden location",
            60);
    }

    [Fact]
    public void CreateDraft_ShouldHaveDraftStatus()
    {
        var game = NewDraft();

        Assert.Equal(GameStatus.Draft, game.Status);
        Assert.Equal("creator-1", game.CreatorUserId);
        Assert.Equal("Treasure Hunt", game.Title);
        Assert.Equal(60, game.DurationMinutes);
    }

    [Fact]
    public void Creator_ShouldBeAbleToEditDraft()
    {
        var game = NewDraft();

        game.UpdateDraft(
            "creator-1",
            "New Game Title",
            "Updated description",
            90);

        Assert.Equal("New Game Title", game.Title);
        Assert.Equal("Updated description", game.Description);
        Assert.Equal(90, game.DurationMinutes);
    }

    [Fact]
    public void OtherUser_ShouldNotEditDraft()
    {
        var game = NewDraft();

        Assert.Throws<UnauthorizedAccessException>(() =>
            game.UpdateDraft(
                "another-user",
                "Hacked Title",
                "Changed description",
                120));

        Assert.Equal("Treasure Hunt", game.Title);
    }

    [Fact]
    public void EmptyTitle_ShouldBeRejected()
    {
        Assert.Throws<ArgumentException>(() =>
            Game.CreateDraft(
                Guid.NewGuid(),
                "creator-1",
                " ",
                null,
                60));
    }

    [Fact]
    public void InvalidDuration_ShouldBeRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Game.CreateDraft(
                Guid.NewGuid(),
                "creator-1",
                "Valid Title",
                null,
                0));
    }

    [Fact]
    public void TitleOver150Characters_ShouldBeRejected()
    {
        var game = NewDraft();

        Assert.Throws<ArgumentException>(() =>
            game.UpdateDraft(
                "creator-1",
                new string('A', 151),
                null,
                60));
    }

    [Fact]
    public void PendingGame_ShouldNotBeEditableAsDraft()
    {
        var game = new Game(
            Guid.NewGuid(),
            "creator-1");

        Assert.Throws<InvalidOperationException>(() =>
            game.UpdateDraft(
                "creator-1",
                "Changed",
                null,
                60));
    }

    [Fact]
    public void Draft_ShouldNotBeApprovedDirectly()
    {
        var game = NewDraft();

        Assert.Throws<InvalidOperationException>(() =>
            game.ApproveCreation(
                new DateTimeOffset(
                    2026, 11, 14, 10, 0, 0,
                    TimeSpan.Zero)));
    }
}
