
using System;
using Wayhunter.Domain.Modules.Games.Entities;
using Xunit;

namespace Wayhunter.UnitTests.Modules.Games;

public sealed class GameAttemptTests
{
    private static readonly DateTimeOffset StartedAt =
        new(2026, 11, 13, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void GameAttempt_ShouldStoreCorrectValues()
    {
        var attemptId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        var attempt = new GameAttempt(
            attemptId,
            gameId,
            "player-001",
            StartedAt);

        Assert.Equal(attemptId, attempt.Id);
        Assert.Equal(gameId, attempt.GameId);
        Assert.Equal("player-001", attempt.PlayerUserId);
        Assert.Equal(StartedAt, attempt.StartedAtUtc);
    }

    [Fact]
    public void GameAttempt_ShouldRejectEmptyAttemptId()
    {
        Assert.Throws<ArgumentException>(() =>
            new GameAttempt(
                Guid.Empty,
                Guid.NewGuid(),
                "player-001",
                StartedAt));
    }

    [Fact]
    public void GameAttempt_ShouldRejectEmptyGameId()
    {
        Assert.Throws<ArgumentException>(() =>
            new GameAttempt(
                Guid.NewGuid(),
                Guid.Empty,
                "player-001",
                StartedAt));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void GameAttempt_ShouldRejectEmptyPlayerId(
        string playerId)
    {
        Assert.Throws<ArgumentException>(() =>
            new GameAttempt(
                Guid.NewGuid(),
                Guid.NewGuid(),
                playerId,
                StartedAt));
    }

    [Fact]
    public void GameAttempt_ShouldRejectNonUtcTimestamp()
    {
        var nonUtcTime = new DateTimeOffset(
            2026, 11, 13, 15, 30, 0,
            TimeSpan.FromHours(5.5));

        Assert.Throws<ArgumentException>(() =>
            new GameAttempt(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "player-001",
                nonUtcTime));
    }

    [Fact]
    public void GameAttempt_ShouldTrimPlayerId()
    {
        var attempt = new GameAttempt(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "  player-001  ",
            StartedAt);

        Assert.Equal("player-001", attempt.PlayerUserId);
    }
}
