
using System;
using System.Collections.Generic;
using Wayhunter.Domain.Modules.Games.Entities;
using Wayhunter.Domain.Modules.Games.Enums;
using Xunit;

namespace Wayhunter.UnitTests.Modules.Games;

public sealed class GamePersistenceRestorationTests
{
    [Fact]
    public void RestorePublishedGame_ShouldPreserveSqlState()
    {
        var gameId = Guid.NewGuid();
        var stepId = Guid.NewGuid();

        var submittedAt = new DateTimeOffset(
            2026, 11, 17, 10, 0, 0, TimeSpan.Zero);

        var approvedAt = submittedAt.AddHours(2);
        var expiresAt = approvedAt.AddHours(720);

        var step = new GameStep(
            stepId, gameId, 1);

        var hint = new GameHint(
            Guid.NewGuid(),
            stepId,
            "Find the location",
            null);

        var game = Game.RestoreFromPersistence(
            id: gameId,
            creatorUserId: "creator-1",
            title: "Test Game",
            description: "SQL restoration test",
            durationMinutes: 60,
            status: GameStatus.Published,
            submittedAtUtc: submittedAt,
            approvedAtUtc: approvedAt,
            expiresAtUtc: expiresAt,
            winnerUserId: "winner-1",
            isFlaggedForReview: false,
            flagReason: null,
            rejectionReason: null,
            disabledReason: null,
            lastModeratedAtUtc: null,
            steps: new[] { step },
            hints: new[] { hint });

        Assert.Equal(gameId, game.Id);
        Assert.Equal("creator-1", game.CreatorUserId);
        Assert.Equal("Test Game", game.Title);
        Assert.Equal(60, game.DurationMinutes);
        Assert.Equal(GameStatus.Published, game.Status);
        Assert.Equal(submittedAt, game.SubmittedAtUtc);
        Assert.Equal(approvedAt, game.ApprovedAtUtc);
        Assert.Equal(expiresAt, game.ExpiresAtUtc);
        Assert.Equal("winner-1", game.WinnerUserId);

        Assert.Single(game.DraftSteps);
        Assert.Single(game.DraftHints);

        Assert.Equal(stepId, game.DraftSteps[0].Id);
        Assert.Equal(stepId, game.DraftHints[0].GameStepId);
    }

    [Fact]
    public void RestoreGame_ShouldRejectInvalidStageOwnership()
    {
        var gameId = Guid.NewGuid();

        var invalidStep = new GameStep(
            Guid.NewGuid(),
            Guid.NewGuid(),
            1);

        Assert.Throws<InvalidOperationException>(() =>
            Game.RestoreFromPersistence(
                id: gameId,
                creatorUserId: "creator-1",
                title: "Test Game",
                description: null,
                durationMinutes: 30,
                status: GameStatus.Draft,
                submittedAtUtc: null,
                approvedAtUtc: null,
                expiresAtUtc: null,
                winnerUserId: null,
                isFlaggedForReview: false,
                flagReason: null,
                rejectionReason: null,
                disabledReason: null,
                lastModeratedAtUtc: null,
                steps: new[] { invalidStep },
                hints: Array.Empty<GameHint>()));
    }

    [Fact]
    public void RestoreGame_ShouldRejectInvalidStatus()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Game.RestoreFromPersistence(
                id: Guid.NewGuid(),
                creatorUserId: "creator-1",
                title: null,
                description: null,
                durationMinutes: null,
                status: (GameStatus)999,
                submittedAtUtc: null,
                approvedAtUtc: null,
                expiresAtUtc: null,
                winnerUserId: null,
                isFlaggedForReview: false,
                flagReason: null,
                rejectionReason: null,
                disabledReason: null,
                lastModeratedAtUtc: null,
                steps: Array.Empty<GameStep>(),
                hints: Array.Empty<GameHint>()));
    }
}
