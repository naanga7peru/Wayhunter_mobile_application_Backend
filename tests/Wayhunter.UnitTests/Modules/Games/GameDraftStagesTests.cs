
using System;
using System.Collections.Generic;
using Wayhunter.Domain.Modules.Games.Entities;
using Xunit;

namespace Wayhunter.UnitTests.Modules.Games;

public sealed class GameDraftStagesTests
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

    private static GameStep NewStep(
        Game game,
        int number)
    {
        return new GameStep(
            Guid.NewGuid(),
            game.Id,
            number);
    }

    private static GameHint NewHint(GameStep step)
    {
        return new GameHint(
            Guid.NewGuid(),
            step.Id,
            "Find the clue",
            null);
    }

    [Fact]
    public void ValidStages_ShouldBeSavedInDraft()
    {
        var game = NewDraft();

        var step1 = NewStep(game, 1);
        var step2 = NewStep(game, 2);

        game.ReplaceDraftStages(
            "creator-1",
            new[] { step1, step2 },
            new[]
            {
                NewHint(step1),
                NewHint(step2)
            });

        Assert.Equal(2, game.DraftSteps.Count);
        Assert.Equal(2, game.DraftHints.Count);
        Assert.Equal(1, game.DraftSteps[0].StepNumber);
        Assert.Equal(2, game.DraftSteps[1].StepNumber);
    }

    [Fact]
    public void OtherUser_ShouldNotEditStages()
    {
        var game = NewDraft();
        var step = NewStep(game, 1);

        Assert.Throws<UnauthorizedAccessException>(() =>
            game.ReplaceDraftStages(
                "another-user",
                new[] { step },
                new[] { NewHint(step) }));

        Assert.Empty(game.DraftSteps);
    }

    [Fact]
    public void EmptyStages_ShouldBeRejected()
    {
        var game = NewDraft();

        Assert.Throws<ArgumentException>(() =>
            game.ReplaceDraftStages(
                "creator-1",
                Array.Empty<GameStep>(),
                Array.Empty<GameHint>()));
    }

    [Fact]
    public void DuplicateStageNumbers_ShouldBeRejected()
    {
        var game = NewDraft();

        var step1 = NewStep(game, 1);
        var step2 = NewStep(game, 1);

        Assert.Throws<ArgumentException>(() =>
            game.ReplaceDraftStages(
                "creator-1",
                new[] { step1, step2 },
                new[]
                {
                    NewHint(step1),
                    NewHint(step2)
                }));
    }

    [Fact]
    public void StageNumberGap_ShouldBeRejected()
    {
        var game = NewDraft();

        var step1 = NewStep(game, 1);
        var step3 = NewStep(game, 3);

        Assert.Throws<ArgumentException>(() =>
            game.ReplaceDraftStages(
                "creator-1",
                new[] { step1, step3 },
                new[]
                {
                    NewHint(step1),
                    NewHint(step3)
                }));
    }

    [Fact]
    public void ForeignGameStage_ShouldBeRejected()
    {
        var game = NewDraft();

        var foreignStep = new GameStep(
            Guid.NewGuid(),
            Guid.NewGuid(),
            1);

        Assert.Throws<ArgumentException>(() =>
            game.ReplaceDraftStages(
                "creator-1",
                new[] { foreignStep },
                new[] { NewHint(foreignStep) }));
    }

    [Fact]
    public void HintForUnknownStage_ShouldBeRejected()
    {
        var game = NewDraft();
        var step = NewStep(game, 1);

        var unknownHint = new GameHint(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Unknown clue",
            null);

        Assert.Throws<ArgumentException>(() =>
            game.ReplaceDraftStages(
                "creator-1",
                new[] { step },
                new[] { unknownHint }));
    }

    [Fact]
    public void StageWithoutHint_ShouldBeRejected()
    {
        var game = NewDraft();
        var step = NewStep(game, 1);

        Assert.Throws<ArgumentException>(() =>
            game.ReplaceDraftStages(
                "creator-1",
                new[] { step },
                Array.Empty<GameHint>()));
    }

    [Fact]
    public void InvalidReplacement_ShouldPreserveOldStages()
    {
        var game = NewDraft();
        var originalStep = NewStep(game, 1);

        game.ReplaceDraftStages(
            "creator-1",
            new[] { originalStep },
            new[] { NewHint(originalStep) });

        var invalidStep = NewStep(game, 2);

        Assert.Throws<ArgumentException>(() =>
            game.ReplaceDraftStages(
                "creator-1",
                new[] { invalidStep },
                new[] { NewHint(invalidStep) }));

        Assert.Single(game.DraftSteps);
        Assert.Equal(originalStep.Id, game.DraftSteps[0].Id);
        Assert.Single(game.DraftHints);
    }
}
