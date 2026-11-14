
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.Games.Dtos;
using Wayhunter.Application.Modules.Games.Interfaces;
using Wayhunter.Application.Modules.Games.Services;
using Wayhunter.Domain.Modules.Games.Entities;
using Wayhunter.Domain.Modules.Games.Enums;
using Xunit;

namespace Wayhunter.UnitTests.Modules.Games;

public sealed class GameDraftServiceTests
{
    // =====================================
    // FAKE REPOSITORY - NO SQL DATABASE
    // =====================================

    private sealed class FakeGameRepository : IGameRepository
    {
        private readonly Dictionary<Guid, Game> _games = new();

        public int AddCount { get; private set; }

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
            AddCount++;

            return Task.CompletedTask;
        }

        public Task UpdateAsync(
            Game game,
            CancellationToken cancellationToken = default)
        {
            _games[game.Id] = game;

            return Task.CompletedTask;
        }
    }

    // =====================================
    // VALID GAME DRAFT REQUEST
    // =====================================

    private static CreateGameDraftDto ValidDraft()
    {
        return new CreateGameDraftDto(
            "Jaffna Treasure Hunt",
            "Find the hidden places",
            60,
            new List<GameDraftStageDto>
            {
                new(1, "Find the first clue", null),
                new(2, null, "photo-002")
            }
        );
    }

    // =====================================
    // TEST 01 - CREATE GAME DRAFT
    // =====================================

    [Fact]
    public async Task CreateDraft_ShouldSaveGameAndStages()
    {
        var repo = new FakeGameRepository();
        var service = new GameService(repo);

        var game = await service.CreateGameDraftAsync(
            "creator-1",
            ValidDraft());

        Assert.NotEqual(Guid.Empty, game.Id);
        Assert.Equal(GameStatus.Draft, game.Status);
        Assert.Equal("creator-1", game.CreatorUserId);

        Assert.Equal("Jaffna Treasure Hunt", game.Title);
        Assert.Equal(60, game.DurationMinutes);

        Assert.Equal(2, game.DraftSteps.Count);
        Assert.Equal(2, game.DraftHints.Count);

        Assert.Equal(1, game.DraftSteps[0].StepNumber);
        Assert.Equal(2, game.DraftSteps[1].StepNumber);

        Assert.Equal(1, repo.AddCount);

        var saved = await repo.GetByIdAsync(game.Id);

        Assert.Same(game, saved);
    }

    // =====================================
    // TEST 02 - CREATOR CAN READ
    // =====================================

    [Fact]
    public async Task Creator_ShouldReadOwnDraft()
    {
        var repo = new FakeGameRepository();
        var service = new GameService(repo);

        var created = await service.CreateGameDraftAsync(
            "creator-1",
            ValidDraft());

        var loaded = await service.GetGameDraftAsync(
            created.Id,
            "creator-1");

        Assert.Same(created, loaded);
        Assert.Equal(GameStatus.Draft, loaded.Status);
    }

    // =====================================
    // TEST 03 - OTHER USER CANNOT READ
    // =====================================

    [Fact]
    public async Task OtherUser_ShouldNotReadDraft()
    {
        var repo = new FakeGameRepository();
        var service = new GameService(repo);

        var game = await service.CreateGameDraftAsync(
            "creator-1",
            ValidDraft());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.GetGameDraftAsync(
                game.Id,
                "another-user"));
    }

    // =====================================
    // TEST 04 - MISSING GAME
    // =====================================

    [Fact]
    public async Task MissingDraft_ShouldThrowNotFound()
    {
        var repo = new FakeGameRepository();
        var service = new GameService(repo);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => service.GetGameDraftAsync(
                Guid.NewGuid(),
                "creator-1"));
    }

    // =====================================
    // TEST 05 - INVALID TITLE
    // =====================================

    [Fact]
    public async Task EmptyTitle_ShouldNotCreateDraft()
    {
        var repo = new FakeGameRepository();
        var service = new GameService(repo);

        var request = ValidDraft() with
        {
            Title = " "
        };

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.CreateGameDraftAsync(
                "creator-1",
                request));

        Assert.Equal(0, repo.AddCount);
    }

    // =====================================
    // TEST 06 - INVALID DURATION
    // =====================================

    [Fact]
    public async Task InvalidDuration_ShouldNotCreateDraft()
    {
        var repo = new FakeGameRepository();
        var service = new GameService(repo);

        var request = ValidDraft() with
        {
            DurationMinutes = 0
        };

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => service.CreateGameDraftAsync(
                "creator-1",
                request));

        Assert.Equal(0, repo.AddCount);
    }

    // =====================================
    // TEST 07 - INVALID STAGE ORDER
    // =====================================

    [Fact]
    public async Task StageNumberGap_ShouldNotSaveDraft()
    {
        var repo = new FakeGameRepository();
        var service = new GameService(repo);

        var request = ValidDraft() with
        {
            Stages = new List<GameDraftStageDto>
            {
                new(1, "First clue", null),
                new(3, "Third clue", null)
            }
        };

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.CreateGameDraftAsync(
                "creator-1",
                request));

        Assert.Equal(0, repo.AddCount);
    }

    // =====================================
    // TEST 08 - PENDING GAME IS NOT A DRAFT
    // =====================================

    [Fact]
    public async Task PendingGame_ShouldNotBeReadAsDraft()
    {
        var repo = new FakeGameRepository();
        var service = new GameService(repo);

        var game = await service.CreatePendingGameAsync(
            "creator-1");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.GetGameDraftAsync(
                game.Id,
                "creator-1"));
    }
}
