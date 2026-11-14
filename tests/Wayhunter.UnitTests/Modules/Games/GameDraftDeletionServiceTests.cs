
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.Games.Interfaces;
using Wayhunter.Application.Modules.Games.Services;
using Wayhunter.Domain.Modules.Games.Entities;
using Xunit;

namespace Wayhunter.UnitTests.Modules.Games;

public sealed class GameDraftDeletionServiceTests
{
    // =====================================
    // FAKE REPOSITORY - NO SQL DATABASE
    // =====================================

    private sealed class FakeGameRepository
        : IGameDraftDeletionRepository
    {
        private readonly Dictionary<Guid, Game> _games = new();

        public int DeleteCount { get; private set; }

        public Task<Game?> GetByIdAsync(
            Guid gameId,
            CancellationToken cancellationToken = default)
        {
            _games.TryGetValue(gameId, out var game);

            return Task.FromResult<Game?>(game);
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
            return Task.CompletedTask;
        }

        public Task DeleteAsync(
            Guid gameId,
            CancellationToken cancellationToken = default)
        {
            if (!_games.Remove(gameId))
                throw new KeyNotFoundException(
                    "Game not found.");

            DeleteCount++;

            return Task.CompletedTask;
        }
    }

    // =====================================
    // CREATE VALID DRAFT FOR TESTING
    // =====================================

    private static Game NewDraft()
    {
        return Game.CreateDraft(
            Guid.NewGuid(),
            "creator-1",
            "Jaffna Treasure Hunt",
            "Find hidden places",
            60);
    }

    // =====================================
    // TEST 01 - CREATOR CAN DELETE
    // =====================================

    [Fact]
    public async Task Creator_ShouldDeleteOwnDraft()
    {
        var repo = new FakeGameRepository();
        var service = new GameDraftDeletionService(repo);
        var game = NewDraft();

        await repo.AddAsync(game);

        await service.DeleteGameDraftAsync(
            game.Id,
            "creator-1");

        var saved = await repo.GetByIdAsync(game.Id);

        Assert.Null(saved);
        Assert.Equal(1, repo.DeleteCount);
    }

    // =====================================
    // TEST 02 - OTHER USER CANNOT DELETE
    // =====================================

    [Fact]
    public async Task OtherUser_ShouldNotDeleteDraft()
    {
        var repo = new FakeGameRepository();
        var service = new GameDraftDeletionService(repo);
        var game = NewDraft();

        await repo.AddAsync(game);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.DeleteGameDraftAsync(
                game.Id,
                "another-user"));

        Assert.NotNull(await repo.GetByIdAsync(game.Id));
        Assert.Equal(0, repo.DeleteCount);
    }

    // =====================================
    // TEST 03 - PENDING GAME CANNOT DELETE
    // =====================================

    [Fact]
    public async Task PendingGame_ShouldNotBeDeleted()
    {
        var repo = new FakeGameRepository();
        var service = new GameDraftDeletionService(repo);

        var game = new Game(
            Guid.NewGuid(),
            "creator-1");

        await repo.AddAsync(game);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.DeleteGameDraftAsync(
                game.Id,
                "creator-1"));

        Assert.NotNull(await repo.GetByIdAsync(game.Id));
        Assert.Equal(0, repo.DeleteCount);
    }

    // =====================================
    // TEST 04 - PUBLISHED GAME CANNOT DELETE
    // =====================================

    [Fact]
    public async Task PublishedGame_ShouldNotBeDeleted()
    {
        var repo = new FakeGameRepository();
        var service = new GameDraftDeletionService(repo);

        var game = new Game(
            Guid.NewGuid(),
            "creator-1");

        game.ApproveCreation(
            new DateTimeOffset(
                2026, 11, 14, 10, 0, 0,
                TimeSpan.Zero));

        await repo.AddAsync(game);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.DeleteGameDraftAsync(
                game.Id,
                "creator-1"));

        Assert.NotNull(await repo.GetByIdAsync(game.Id));
        Assert.Equal(0, repo.DeleteCount);
    }

    // =====================================
    // TEST 05 - REJECTED GAME CANNOT DELETE
    // =====================================

    [Fact]
    public async Task RejectedGame_ShouldNotBeDeleted()
    {
        var repo = new FakeGameRepository();
        var service = new GameDraftDeletionService(repo);

        var game = new Game(
            Guid.NewGuid(),
            "creator-1");

        game.RejectCreation();

        await repo.AddAsync(game);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.DeleteGameDraftAsync(
                game.Id,
                "creator-1"));

        Assert.Equal(0, repo.DeleteCount);
    }

    // =====================================
    // TEST 06 - MISSING GAME
    // =====================================

    [Fact]
    public async Task MissingGame_ShouldThrowNotFound()
    {
        var repo = new FakeGameRepository();
        var service = new GameDraftDeletionService(repo);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => service.DeleteGameDraftAsync(
                Guid.NewGuid(),
                "creator-1"));

        Assert.Equal(0, repo.DeleteCount);
    }

    // =====================================
    // TEST 07 - EMPTY GAME ID
    // =====================================

    [Fact]
    public async Task EmptyGameId_ShouldBeRejected()
    {
        var repo = new FakeGameRepository();
        var service = new GameDraftDeletionService(repo);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.DeleteGameDraftAsync(
                Guid.Empty,
                "creator-1"));

        Assert.Equal(0, repo.DeleteCount);
    }

    // =====================================
    // TEST 08 - EMPTY USER ID
    // =====================================

    [Fact]
    public async Task EmptyUserId_ShouldBeRejected()
    {
        var repo = new FakeGameRepository();
        var service = new GameDraftDeletionService(repo);
        var game = NewDraft();

        await repo.AddAsync(game);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.DeleteGameDraftAsync(
                game.Id,
                "  "));

        Assert.NotNull(await repo.GetByIdAsync(game.Id));
        Assert.Equal(0, repo.DeleteCount);
    }

    // =====================================
    // TEST 09 - REPEATED DELETION
    // =====================================

    [Fact]
    public async Task DeleteTwice_ShouldNotDeleteAgain()
    {
        var repo = new FakeGameRepository();
        var service = new GameDraftDeletionService(repo);
        var game = NewDraft();

        await repo.AddAsync(game);

        await service.DeleteGameDraftAsync(
            game.Id,
            "creator-1");

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => service.DeleteGameDraftAsync(
                game.Id,
                "creator-1"));

        Assert.Equal(1, repo.DeleteCount);
    }
}
