
using System;
using System.Collections.Generic;
using Wayhunter.Application.Modules.Games.Dtos;
using Wayhunter.Application.Modules.Games.Validators;
using Xunit;

namespace Wayhunter.UnitTests.Modules.Games;

public sealed class GameDraftValidatorTests
{
    private static CreateGameDraftDto ValidDraft()
    {
        return new CreateGameDraftDto(
            "Find the Hidden Place",
            "A location hunting game",
            60,
            new List<GameDraftStageDto>
            {
                new(1, "Find the first clue", null),
                new(2, null, "photo-001")
            }
        );
    }

    [Fact]
    public void ValidDraft_ShouldPass()
    {
        var draft = ValidDraft();

        GameDraftValidator.Validate(draft);
    }

    [Fact]
    public void NullDraft_ShouldReject()
    {
        Assert.Throws<ArgumentNullException>(() =>
            GameDraftValidator.Validate(null));
    }

    [Fact]
    public void EmptyTitle_ShouldReject()
    {
        var draft = ValidDraft() with
        {
            Title = " "
        };

        Assert.Throws<ArgumentException>(() =>
            GameDraftValidator.Validate(draft));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void InvalidDuration_ShouldReject(int duration)
    {
        var draft = ValidDraft() with
        {
            DurationMinutes = duration
        };

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            GameDraftValidator.Validate(draft));
    }

    [Fact]
    public void EmptyStages_ShouldReject()
    {
        var draft = ValidDraft() with
        {
            Stages = Array.Empty<GameDraftStageDto>()
        };

        Assert.Throws<ArgumentException>(() =>
            GameDraftValidator.Validate(draft));
    }

    [Fact]
    public void DuplicateStages_ShouldReject()
    {
        var draft = ValidDraft() with
        {
            Stages = new List<GameDraftStageDto>
            {
                new(1, "First clue", null),
                new(1, "Second clue", null)
            }
        };

        Assert.Throws<ArgumentException>(() =>
            GameDraftValidator.Validate(draft));
    }

    [Fact]
    public void MissingHint_ShouldReject()
    {
        var draft = ValidDraft() with
        {
            Stages = new List<GameDraftStageDto>
            {
                new(1, null, null)
            }
        };

        Assert.Throws<ArgumentException>(() =>
            GameDraftValidator.Validate(draft));
    }
}
