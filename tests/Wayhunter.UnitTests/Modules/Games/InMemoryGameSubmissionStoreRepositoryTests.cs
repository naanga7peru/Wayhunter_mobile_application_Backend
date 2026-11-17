
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.Games.Interfaces;
using Wayhunter.Domain.Modules.Games.Entities;
using Wayhunter.Infrastructure.Persistence.Repositories.Games;
using Xunit;

namespace Wayhunter.UnitTests.Modules.Games;

public sealed class InMemoryGameSubmissionStoreRepositoryTests
{
    private static Game NewDraft(
        Guid? id = null,
        string creatorId = "creator-001")
    {
        return Game.CreateDraft(
            id ?? Guid.NewGuid(),
            creatorId,
            "Jaffna Treasure Hunt",
            "Find hidden locations",
            60);
    }

    [Fact]
    public async Task AddAsync_ShouldSaveGame()
    {
        IGameRepository repository =
            new InMemoryGameSubmissionStore();

        var game = NewDraft();

        await repository.AddAsync(game);

        Assert.Same(game,
            await repository.GetByIdAsync(game.Id));
    }

    [Fact]
    public async Task AddAsync_DuplicateId_ShouldThrow()
    {
        IGameRepository repository =
            new InMemoryGameSubmissionStore();

        var game = NewDraft();

        await repository.AddAsync(game);

        await Assert.ThrowsAsync<ArgumentException>(
            () => repository.AddAsync(game));
    }

    [Fact]
    public async Task GetByIdAsync_MissingGame_ShouldReturnNull()
    {
        IGameRepository repository =
            new InMemoryGameSubmissionStore();

        Assert.Null(await repository.GetByIdAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetByIdAsync_EmptyId_ShouldThrow()
    {
        IGameRepository repository =
            new InMemoryGameSubmissionStore();

        await Assert.ThrowsAsync<ArgumentException>(
            () => repository.GetByIdAsync(Guid.Empty));
    }

    [Fact]
    public async Task UpdateAsync_ShouldUpdateExistingGame()
    {
        IGameRepository repository =
            new InMemoryGameSubmissionStore();

        var game = NewDraft();
        await repository.AddAsync(game);

        var replacement = NewDraft(
            game.Id,
            game.CreatorUserId);

        await repository.UpdateAsync(replacement);

        Assert.Same(
            replacement,
            await repository.GetByIdAsync(game.Id));
    }

    [Fact]
    public async Task UpdateAsync_MissingGame_ShouldThrow()
    {
        IGameRepository repository =
            new InMemoryGameSubmissionStore();

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => repository.UpdateAsync(NewDraft()));
    }

    [Fact]
    public async Task UpdateAsync_DifferentCreator_ShouldThrow()
    {
        IGameRepository repository =
            new InMemoryGameSubmissionStore();

        var game = NewDraft();
        await repository.AddAsync(game);

        var replacement = NewDraft(
            game.Id,
            "another-creator");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => repository.UpdateAsync(replacement));

        Assert.Same(game,
            await repository.GetByIdAsync(game.Id));
    }

    [Fact]
    public async Task DeleteAsync_ShouldDeleteOwnDraft()
    {
        var store = new InMemoryGameSubmissionStore();
        IGameDraftDeletionRepository repository = store;

        var game = NewDraft();
        await repository.AddAsync(game);
        await repository.DeleteAsync(game.Id);

        Assert.Null(await repository.GetByIdAsync(game.Id));
    }

    [Fact]
    public async Task DeleteAsync_PendingGame_ShouldThrow()
    {
        var store = new InMemoryGameSubmissionStore();
        IGameDraftDeletionRepository repository = store;

        var game = new Game(Guid.NewGuid(), "creator-001");
        await repository.AddAsync(game);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => repository.DeleteAsync(game.Id));

        Assert.Same(game,
            await repository.GetByIdAsync(game.Id));
    }

    [Fact]
    public async Task DeleteAsync_MissingGame_ShouldThrow()
    {
        IGameDraftDeletionRepository repository =
            new InMemoryGameSubmissionStore();

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => repository.DeleteAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task CancelledToken_ShouldBeRespected()
    {
        IGameRepository repository =
            new InMemoryGameSubmissionStore();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => repository.AddAsync(NewDraft(), cts.Token));
    }
}
