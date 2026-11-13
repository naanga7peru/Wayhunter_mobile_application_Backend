
using System;
using Wayhunter.Domain.Modules.Games.Entities;
using Xunit;

namespace Wayhunter.UnitTests.Modules.Games;

public sealed class GameStepHintTests
{
    [Fact]
    public void GameStep_ShouldStoreCorrectValues()
    {
        var stepId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        var step = new GameStep(stepId, gameId, 1);

        Assert.Equal(stepId, step.Id);
        Assert.Equal(gameId, step.GameId);
        Assert.Equal(1, step.StepNumber);
    }

    [Fact]
    public void GameStep_ShouldRejectEmptyId()
    {
        Assert.Throws<ArgumentException>(() =>
            new GameStep(Guid.Empty, Guid.NewGuid(), 1));
    }

    [Fact]
    public void GameStep_ShouldRejectEmptyGameId()
    {
        Assert.Throws<ArgumentException>(() =>
            new GameStep(Guid.NewGuid(), Guid.Empty, 1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void GameStep_ShouldRejectInvalidStepNumber(
        int number)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new GameStep(
                Guid.NewGuid(),
                Guid.NewGuid(),
                number));
    }

    [Fact]
    public void GameHint_ShouldAllowTextOnly()
    {
        var hint = new GameHint(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Find the old building",
            null);

        Assert.Equal(
            "Find the old building",
            hint.TextHint);

        Assert.Null(hint.PhotoMediaId);
    }

    [Fact]
    public void GameHint_ShouldAllowPhotoOnly()
    {
        var hint = new GameHint(
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            "photo-001");

        Assert.Null(hint.TextHint);
        Assert.Equal("photo-001", hint.PhotoMediaId);
    }

    [Fact]
    public void GameHint_ShouldRejectEmptyHints()
    {
        Assert.Throws<ArgumentException>(() =>
            new GameHint(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "   ",
                null));
    }

    [Fact]
    public void GameHint_ShouldTrimTextAndPhoto()
    {
        var hint = new GameHint(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "  Look near the gate  ",
            "  photo-002  ");

        Assert.Equal(
            "Look near the gate",
            hint.TextHint);

        Assert.Equal(
            "photo-002",
            hint.PhotoMediaId);
    }

    [Fact]
    public void GameHint_ShouldRejectEmptyStepId()
    {
        Assert.Throws<ArgumentException>(() =>
            new GameHint(
                Guid.NewGuid(),
                Guid.Empty,
                "Find the tree",
                null));
    }
}
