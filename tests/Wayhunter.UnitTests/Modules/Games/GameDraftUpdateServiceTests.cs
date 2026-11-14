
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.Games.Dtos;
using Wayhunter.Application.Modules.Games.Interfaces;
using Wayhunter.Application.Modules.Games.Services;
using Wayhunter.Domain.Modules.Games.Entities;
using Xunit;

namespace Wayhunter.UnitTests.Modules.Games;

public sealed class GameDraftUpdateServiceTests
{
    // =====================================
    // FAKE REPOSITORY - NO SQL DATABASE
    // =====================================

    private sealed class FakeGameRepository : IGameRepository
    {
        private readonly Dictionary<Guid, Game> _games = new();

        public int UpdateCount { get; private set; }

        public Task<Game?> GetByIdAsync(
            Guid gameId,
            CancellationToken cancellationToken = default)
        {
            _games.TryGetValue(gameId, out var game);
            return Task.FromResult(game);
        }

        public Task AddAsync(
            Game game,
            CancellationToken cancellationToken = default)
        {
            _games.Add(game.Id, game);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(
            Game game,
            CancellationToken cancellationToken = default)
        {
            _games[game.Id] = game;
            UpdateCount++;
            return Task.CompletedTask;
        }
    }

    // =====================================
    // VALID REQUEST
    // =====================================

    private static CreateGameDraftDto ValidDraft()
    {
        return new CreateGameDraftDto(
            "Jaffna Treasure Hunt",
            "Original description",
            60,
            new List<GameDraftStageDto>
            {
                new(1, "Original clue", null)
            });
    }

    private static CreateGameDraftDto UpdatedDraft()
    {
        return new CreateGameDraftDto(
            "Updated Treasure Hunt",
            "Updated description",
            90,
            new List<GameDraftStageDto>
            {
                new(1, "New clue one", null),
                new(2, null, "photo-002")
            });
    }

    // =====================================
    // TEST 01 - CREATOR CAN UPDATE
    // =====================================

    [Fact]
    public async Task Creator_ShouldUpdateOwnDraft()
    {
        var repo = new FakeGameRepository();
        var service = new GameService(repo);

        var game = await service.CreateGameDraftAsync(
            "creator-1",
            ValidDraft());

        var updated = await service.UpdateGameDraftAsync(
            game.Id,
            "creator-1",
            UpdatedDraft());

        Assert.Equal(
            "Updated Treasure Hunt",
            updated.Title);

        Assert.Equal(
            "Updated description",
            updated.Description);

        Assert.Equal(90, updated.DurationMinutes);
        Assert.Equal(2, updated.DraftSteps.Count);
        Assert.Equal(2, updated.DraftHints.Count);

        Assert.Equal(1, repo.UpdateCount);
    }

    // =====================================
    // TEST 02 - OTHER USER CANNOT UPDATE
    // =====================================

    [Fact]
    public async Task OtherUser_ShouldNotUpdateDraft()
    {
        var repo = new FakeGameRepository();
        var service = new GameService(repo);

        var game = await service.CreateGameDraftAsync(
            "creator-1",
            ValidDraft());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.UpdateGameDraftAsync(
                game.Id,
                "another-user",
                UpdatedDraft()));

        Assert.Equal(
            "Jaffna Treasure Hunt",
            game.Title);

        Assert.Equal(0, repo.UpdateCount);
    }

    // =====================================
    // TEST 03 - EMPTY TITLE
    // =====================================

    [Fact]
    public async Task EmptyTitle_ShouldNotChangeDraft()
    {
        var repo = new FakeGameRepository();
        var service = new GameService(repo);

        var game = await service.CreateGameDraftAsync(
            "creator-1",
            ValidDraft());

        var invalidRequest = UpdatedDraft() with
        {
            Title = " "
        };

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.UpdateGameDraftAsync(
                game.Id,
                "creator-1",
                invalidRequest));

        Assert.Equal(
            "Jaffna Treasure Hunt",
            game.Title);

        Assert.Equal(60, game.DurationMinutes);
        Assert.Single(game.DraftSteps);
        Assert.Equal(0, repo.UpdateCount);
    }

    // =====================================
    // TEST 04 - INVALID STAGE ORDER
    // =====================================

    [Fact]
    public async Task StageGap_ShouldPreserveOldStages()
    {
        var repo = new FakeGameRepository();
        var service = new GameService(repo);

        var game = await service.CreateGameDraftAsync(
            "creator-1",
            ValidDraft());

        var originalStepId = game.DraftSteps[0].Id;

        var invalidRequest = UpdatedDraft() with
        {
            Stages = new List<GameDraftStageDto>
            {
                new(2, "Invalid stage order", null)
            }
        };

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.UpdateGameDraftAsync(
                game.Id,
                "creator-1",
                invalidRequest));

        Assert.Equal(
            originalStepId,
            game.DraftSteps[0].Id);

        Assert.Equal(
            "Jaffna Treasure Hunt",
            game.Title);

        Assert.Equal(0, repo.UpdateCount);
    }

    // =====================================
    // TEST 05 - MISSING HINT
    // =====================================

    [Fact]
    public async Task MissingHint_ShouldNotUpdateDraft()
    {
        var repo = new FakeGameRepository();
        var service = new GameService(repo);

        var game = await service.CreateGameDraftAsync(
            "creator-1",
            ValidDraft());

        var invalidRequest = UpdatedDraft() with
        {
            Stages = new List<GameDraftStageDto>
            {
                new(1, null, null)
            }
        };

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.UpdateGameDraftAsync(
                game.Id,
                "creator-1",
                invalidRequest));

        Assert.Single(game.DraftSteps);
        Assert.Equal(0, repo.UpdateCount);
    }

    // =====================================
    // TEST 06 - PENDING GAME CANNOT UPDATE
    // =====================================

    [Fact]
    public async Task PendingGame_ShouldNotBeUpdated()
    {
        var repo = new FakeGameRepository();
        var service = new GameService(repo);

        var game = await service.CreatePendingGameAsync(
            "creator-1");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.UpdateGameDraftAsync(
                game.Id,
                "creator-1",
                UpdatedDraft()));

        Assert.Equal(0, repo.UpdateCount);
    }

    // =====================================
    // TEST 07 - MISSING GAME
    // =====================================

    [Fact]
    public async Task MissingGame_ShouldThrowNotFound()
    {
        var repo = new FakeGameRepository();
        var service = new GameService(repo);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => service.UpdateGameDraftAsync(
                Guid.NewGuid(),
                "creator-1",
                UpdatedDraft()));

        Assert.Equal(0, repo.UpdateCount);
    }

    // =====================================
    // TEST 08 - INVALID GAME ID
    // =====================================

    [Fact]
    public async Task EmptyGameId_ShouldBeRejected()
    {
        var repo = new FakeGameRepository();
        var service = new GameService(repo);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.UpdateGameDraftAsync(
                Guid.Empty,
                "creator-1",
                UpdatedDraft()));

        Assert.Equal(0, repo.UpdateCount);
    }
}
