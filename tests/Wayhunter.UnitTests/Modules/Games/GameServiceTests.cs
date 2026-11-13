
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.Games.Interfaces;
using Wayhunter.Application.Modules.Games.Services;
using Wayhunter.Domain.Modules.Games.Entities;
using Wayhunter.Domain.Modules.Games.Enums;
using Xunit;

namespace Wayhunter.UnitTests.Modules.Games;

public sealed class GameServiceTests
{
    private static readonly DateTimeOffset ApprovedAt =
        new(2026, 11, 13, 10, 0, 0, TimeSpan.Zero);

    private sealed class FakeGameRepository : IGameRepository
    {
        private readonly Dictionary<Guid, Game> _games = new();

        public int AddCount { get; private set; }
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
            AddCount++;
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

    [Fact]
    public void Constructor_ShouldRejectNullRepository()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new GameService(null!));
    }

    [Fact]
    public async Task CreateGame_ShouldBePendingApproval()
    {
        var repo = new FakeGameRepository();
        var service = new GameService(repo);

        var game = await service.CreatePendingGameAsync(
            "creator-1");

        Assert.NotEqual(Guid.Empty, game.Id);
        Assert.Equal("creator-1", game.CreatorUserId);
        Assert.Equal(GameStatus.PendingApproval, game.Status);
        Assert.Equal(1, repo.AddCount);

        var saved = await repo.GetByIdAsync(game.Id);
        Assert.Same(game, saved);
    }

    [Fact]
    public async Task CreateGame_ShouldRejectEmptyCreator()
    {
        var repo = new FakeGameRepository();
        var service = new GameService(repo);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreatePendingGameAsync("   "));

        Assert.Equal(0, repo.AddCount);
    }

    [Fact]
    public async Task ApproveGame_ShouldPublishFor30Days()
    {
        var repo = new FakeGameRepository();
        var service = new GameService(repo);

        var game = await service.CreatePendingGameAsync(
            "creator-1");

        await service.ApproveGameCreationAsync(
            game.Id, ApprovedAt);

        Assert.Equal(GameStatus.Published, game.Status);
        Assert.Equal(ApprovedAt, game.ApprovedAtUtc);
        Assert.Equal(
            ApprovedAt.AddDays(30),
            game.ExpiresAtUtc);

        Assert.Equal(1, repo.UpdateCount);
    }

    [Fact]
    public async Task ApproveMissingGame_ShouldThrow()
    {
        var service = new GameService(
            new FakeGameRepository());

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.ApproveGameCreationAsync(
                Guid.NewGuid(), ApprovedAt));
    }

    [Fact]
    public async Task ApproveEmptyGameId_ShouldThrow()
    {
        var service = new GameService(
            new FakeGameRepository());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ApproveGameCreationAsync(
                Guid.Empty, ApprovedAt));
    }

    [Fact]
    public async Task ApproveGameTwice_ShouldThrow()
    {
        var repo = new FakeGameRepository();
        var service = new GameService(repo);

        var game = await service.CreatePendingGameAsync(
            "creator-1");

        await service.ApproveGameCreationAsync(
            game.Id, ApprovedAt);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ApproveGameCreationAsync(
                game.Id, ApprovedAt.AddMinutes(1)));

        Assert.Equal(1, repo.UpdateCount);
    }

    [Fact]
    public async Task NonUtcApproval_ShouldBeRejected()
    {
        var repo = new FakeGameRepository();
        var service = new GameService(repo);

        var game = await service.CreatePendingGameAsync(
            "creator-1");

        var nonUtcTime = ApprovedAt.ToOffset(
            TimeSpan.FromHours(5.5));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ApproveGameCreationAsync(
                game.Id, nonUtcTime));

        Assert.Equal(
            GameStatus.PendingApproval,
            game.Status);

        Assert.Equal(0, repo.UpdateCount);
    }
}
