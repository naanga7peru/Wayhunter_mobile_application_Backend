
using System;
using Wayhunter.Domain.Modules.Games.Entities;
using Wayhunter.Domain.Modules.Games.Enums;
using Xunit;

namespace Wayhunter.UnitTests.Modules.Games;

public class GameTests
{
    private static readonly DateTimeOffset ApprovalTime =
        new(2026, 11, 13, 10, 0, 0, TimeSpan.Zero);

    private static Game CreateGame()
    {
        return new Game(Guid.NewGuid(), "creator-1");
    }

    [Fact]
    public void NewGame_ShouldBePending()
    {
        var game = CreateGame();

        Assert.Equal(GameStatus.PendingApproval, game.Status);
        Assert.False(game.CanPlay(ApprovalTime));
    }

    [Fact]
    public void ApprovedGame_ShouldExpireAfter30Days()
    {
        var game = CreateGame();

        game.ApproveCreation(ApprovalTime);

        Assert.Equal(GameStatus.Published, game.Status);
        Assert.Equal(
            ApprovalTime.AddDays(30),
            game.ExpiresAtUtc);
    }

    [Fact]
    public void Game_ShouldNotBePlayableAtExpiry()
    {
        var game = CreateGame();
        game.ApproveCreation(ApprovalTime);

        Assert.True(game.CanPlay(
            ApprovalTime.AddDays(30).AddTicks(-1)));

        Assert.False(game.CanPlay(
            ApprovalTime.AddDays(30)));
    }

    [Fact]
    public void Creator_ShouldNotWinOwnGame()
    {
        var game = CreateGame();
        game.ApproveCreation(ApprovalTime);

        Assert.Throws<InvalidOperationException>(() =>
            game.ApproveVerifiedWinner(
                "creator-1", ApprovalTime));
    }

    [Fact]
    public void Game_ShouldHaveOnlyOneWinner()
    {
        var game = CreateGame();
        game.ApproveCreation(ApprovalTime);

        game.ApproveVerifiedWinner(
            "player-1", ApprovalTime);

        Assert.Equal("player-1", game.WinnerUserId);

        Assert.Throws<InvalidOperationException>(() =>
            game.ApproveVerifiedWinner(
                "player-2", ApprovalTime));
    }

    [Fact]
    public void ExpiredGame_ShouldBeArchived()
    {
        var game = CreateGame();
        game.ApproveCreation(ApprovalTime);

        bool archived = game.ArchiveIfExpired(
            ApprovalTime.AddDays(30));

        Assert.True(archived);
        Assert.Equal(GameStatus.Expired, game.Status);
        Assert.False(game.CanPlay(
            ApprovalTime.AddDays(31)));
    }

    [Fact]
    public void Archive_ShouldBeIdempotent()
    {
        var game = CreateGame();
        game.ApproveCreation(ApprovalTime);

        Assert.True(game.ArchiveIfExpired(
            ApprovalTime.AddDays(30)));

        Assert.False(game.ArchiveIfExpired(
            ApprovalTime.AddDays(31)));
    }
}
