
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.Games.Interfaces;
using Wayhunter.Application.Modules.Games.Services;
using Wayhunter.Application.Modules.IdentitySocial.Dtos;
using Wayhunter.Domain.Modules.Games.Entities;
using Wayhunter.Domain.Modules.Games.Enums;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Wayhunter.Infrastructure.Persistence.Repositories.Games;
using Xunit;

namespace Wayhunter.UnitTests.Modules.Games;

public sealed class InMemoryGameSubmissionStoreTests
{
    private const string CreatorId = "creator-001";

    private static readonly DateTimeOffset CreatedAt =
        new(2026, 11, 14, 5, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset SubmittedAt =
        CreatedAt.AddHours(2);

    // =====================================
    // CREATE VALID GAME DRAFT
    // =====================================

    private static Game NewDraft(
        string creatorId = CreatorId)
    {
        var game = Game.CreateDraft(
            Guid.NewGuid(),
            creatorId,
            "Jaffna Treasure Hunt",
            "Find the hidden location",
            60);

        var step = new GameStep(
            Guid.NewGuid(),
            game.Id,
            1);

        var hint = new GameHint(
            Guid.NewGuid(),
            step.Id,
            "Find the first place",
            null);

        game.ReplaceDraftStages(
            creatorId,
            new[] { step },
            new[] { hint });

        return game;
    }

    private static ApprovalIdentitySnapshotDto Snapshot(
        Game game,
        DateTimeOffset? timestamp = null)
    {
        return new ApprovalIdentitySnapshotDto(
            game.CreatorUserId,
            "Thuvarathan",
            game.Id,
            timestamp ?? SubmittedAt);
    }

    // TEST 01 - SUCCESSFUL SUBMISSION

    [Fact]
    public async Task ValidDraft_ShouldEnterAdminQueue()
    {
        var store = new InMemoryGameSubmissionStore();
        var game = NewDraft();

        store.AddDraft(game);

        var result = await store.SubmitDraftAndEnqueueAsync(
            Snapshot(game));

        Assert.Equal(
            GameStatus.PendingApproval,
            game.Status);

        Assert.Equal(
            SubmittedAt,
            game.SubmittedAtUtc);

        Assert.Equal(game.Id, result.GameId);
        Assert.Equal(CreatorId, result.CreatorUserId);

        Assert.Equal(
            "Thuvarathan",
            result.CreatorNameSnapshot);

        Assert.Null(game.ApprovedAtUtc);
        Assert.Null(game.ExpiresAtUtc);

        Assert.Single(store.GetPendingItems());
    }

    // TEST 02 - DUPLICATE SUBMISSION

    [Fact]
    public async Task DuplicateSubmission_ShouldBeRejected()
    {
        var store = new InMemoryGameSubmissionStore();
        var game = NewDraft();

        store.AddDraft(game);

        var identity = Snapshot(game);

        await store.SubmitDraftAndEnqueueAsync(identity);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.SubmitDraftAndEnqueueAsync(
                identity));

        Assert.Single(store.GetPendingItems());

        Assert.Equal(
            SubmittedAt,
            game.SubmittedAtUtc);
    }

    // TEST 03 - DIFFERENT USER CANNOT SUBMIT

    [Fact]
    public async Task WrongCreator_ShouldBeRejected()
    {
        var store = new InMemoryGameSubmissionStore();
        var game = NewDraft();

        store.AddDraft(game);

        var wrongIdentity = new ApprovalIdentitySnapshotDto(
            "another-user",
            "Another Player",
            game.Id,
            SubmittedAt);

        await Assert.ThrowsAsync<
            UnauthorizedAccessException>(
            () => store.SubmitDraftAndEnqueueAsync(
                wrongIdentity));

        Assert.Equal(GameStatus.Draft, game.Status);
        Assert.Empty(store.GetPendingItems());
    }

    // TEST 04 - GAME NOT FOUND

    [Fact]
    public async Task MissingGame_ShouldBeRejected()
    {
        var store = new InMemoryGameSubmissionStore();

        var identity = new ApprovalIdentitySnapshotDto(
            CreatorId,
            "Thuvarathan",
            Guid.NewGuid(),
            SubmittedAt);

        await Assert.ThrowsAsync<
            System.Collections.Generic.KeyNotFoundException>(
            () => store.SubmitDraftAndEnqueueAsync(
                identity));

        Assert.Empty(store.GetPendingItems());
    }

    // TEST 05 - INCOMPLETE DRAFT

    [Fact]
    public async Task DraftWithoutStages_ShouldNotEnterQueue()
    {
        var store = new InMemoryGameSubmissionStore();

        var game = Game.CreateDraft(
            Guid.NewGuid(),
            CreatorId,
            "Incomplete Game",
            null,
            60);

        store.AddDraft(game);

        await Assert.ThrowsAsync<
            InvalidOperationException>(
            () => store.SubmitDraftAndEnqueueAsync(
                Snapshot(game)));

        Assert.Equal(GameStatus.Draft, game.Status);
        Assert.Null(game.SubmittedAtUtc);

        Assert.Empty(store.GetPendingItems());
    }

    // TEST 06 - ONLY DRAFTS CAN BE REGISTERED

    [Fact]
    public void PendingGame_ShouldNotBeRegisteredAsDraft()
    {
        var store = new InMemoryGameSubmissionStore();

        var game = new Game(
            Guid.NewGuid(),
            CreatorId);

        Assert.Throws<InvalidOperationException>(
            () => store.AddDraft(game));

        Assert.Empty(store.GetPendingItems());
    }

    // TEST 07 - INVALID IDENTITY

    [Fact]
    public async Task InvalidIdentity_ShouldBeRejected()
    {
        var store = new InMemoryGameSubmissionStore();
        var game = NewDraft();

        store.AddDraft(game);

        var invalid = new ApprovalIdentitySnapshotDto(
            CreatorId,
            " ",
            game.Id,
            SubmittedAt);

        await Assert.ThrowsAsync<ArgumentException>(
            () => store.SubmitDraftAndEnqueueAsync(
                invalid));

        Assert.Equal(GameStatus.Draft, game.Status);
        Assert.Empty(store.GetPendingItems());
    }

    // TEST 08 - UTC TIMESTAMP REQUIRED

    [Fact]
    public async Task NonUtcTimestamp_ShouldBeRejected()
    {
        var store = new InMemoryGameSubmissionStore();
        var game = NewDraft();

        store.AddDraft(game);

        var invalidTime = new DateTimeOffset(
            2026, 11, 14, 12, 0, 0,
            TimeSpan.FromMinutes(330));

        var identity = Snapshot(game, invalidTime);

        await Assert.ThrowsAsync<ArgumentException>(
            () => store.SubmitDraftAndEnqueueAsync(
                identity));

        Assert.Equal(GameStatus.Draft, game.Status);
        Assert.Empty(store.GetPendingItems());
    }

    // TEST 09 - CANCELLATION

    [Fact]
    public async Task CancelledSubmission_ShouldNotChangeGame()
    {
        var store = new InMemoryGameSubmissionStore();
        var game = NewDraft();

        store.AddDraft(game);

        using var cancellation =
            new CancellationTokenSource();

        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<
            OperationCanceledException>(
            () => store.SubmitDraftAndEnqueueAsync(
                Snapshot(game),
                cancellation.Token));

        Assert.Equal(GameStatus.Draft, game.Status);
        Assert.Empty(store.GetPendingItems());
    }

    // TEST 10 - QUEUE ORDER

    [Fact]
    public async Task Queue_ShouldBeOrderedBySubmissionTime()
    {
        var store = new InMemoryGameSubmissionStore();

        var laterGame = NewDraft();
        var earlierGame = NewDraft();

        store.AddDraft(laterGame);
        store.AddDraft(earlierGame);

        await store.SubmitDraftAndEnqueueAsync(
            Snapshot(laterGame, SubmittedAt.AddMinutes(20)));

        await store.SubmitDraftAndEnqueueAsync(
            Snapshot(earlierGame, SubmittedAt));

        var items = store.GetPendingItems();

        Assert.Equal(2, items.Count);
        Assert.Equal(earlierGame.Id, items[0].GameId);
        Assert.Equal(laterGame.Id, items[1].GameId);
    }

    // TEST 11 - CONCURRENT SUBMISSION

    [Fact]
    public async Task ConcurrentSubmissions_ShouldAllowOnlyOne()
    {
        var store = new InMemoryGameSubmissionStore();
        var game = NewDraft();

        store.AddDraft(game);

        var identity = Snapshot(game);

        var attempts = Enumerable.Range(0, 12)
            .Select(_ => Task.Run(async () =>
            {
                try
                {
                    await store.SubmitDraftAndEnqueueAsync(
                        identity);

                    return true;
                }
                catch (InvalidOperationException)
                {
                    return false;
                }
            }))
            .ToArray();

        var results = await Task.WhenAll(attempts);

        Assert.Equal(1, results.Count(success => success));
        Assert.Single(store.GetPendingItems());

        Assert.Equal(
            GameStatus.PendingApproval,
            game.Status);
    }

    // TEST 12 - SERVICE + STORE INTEGRATION

    [Fact]
    public async Task ActiveCreator_ShouldSubmitThroughService()
    {
        var creator = UserAccount.RegisterPending(
            "Thuvarathan",
            "creator@example.com",
            "test-password-hash",
            CreatedAt);

        creator.ActivateAfterVerifiedOtp(
            CreatedAt.AddMinutes(10));

        var game = NewDraft(creator.UserId);

        var store = new InMemoryGameSubmissionStore();
        store.AddDraft(game);

        var service = new GameSubmissionService(store);

        var result = await service.SubmitAsync(
            game.Id,
            creator,
            SubmittedAt);

        Assert.Equal(game.Id, result.GameId);

        Assert.Equal(
            creator.UserId,
            result.CreatorUserId);

        Assert.Equal(
            GameStatus.PendingApproval,
            game.Status);

        Assert.Single(store.GetPendingItems());
    }
}
