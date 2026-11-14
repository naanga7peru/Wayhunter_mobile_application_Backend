
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.CoinsWallet.Validators;
using Wayhunter.Application.Modules.Games.Dtos;
using Wayhunter.Application.Modules.Games.Services;
using Wayhunter.Application.Modules.IdentitySocial.Dtos;
using Wayhunter.Domain.Modules.CoinsWallet.Enums;
using Wayhunter.Domain.Modules.Games.Entities;
using Wayhunter.Domain.Modules.Games.Enums;
using Wayhunter.Infrastructure.Persistence.Repositories.Games;
using Xunit;

namespace Wayhunter.UnitTests.Modules.Games;

public sealed class GameApprovalWorkflowTests
{
    private const string CreatorId = "creator-001";

    private static readonly DateTimeOffset SubmittedAtUtc =
        new(2026, 11, 14, 6, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset ApprovedAtUtc =
        SubmittedAtUtc.AddHours(2);

    // =====================================
    // CREATE VALID DRAFT
    // =====================================

    private static Game NewDraft()
    {
        var game = Game.CreateDraft(
            Guid.NewGuid(),
            CreatorId,
            "Jaffna Treasure Hunt",
            "Find the hidden treasure",
            60);

        var step = new GameStep(
            Guid.NewGuid(),
            game.Id,
            1);

        var hint = new GameHint(
            Guid.NewGuid(),
            step.Id,
            "Find the first location",
            null);

        game.ReplaceDraftStages(
            CreatorId,
            new[] { step },
            new[] { hint });

        return game;
    }

    // =====================================
    // CREATE VALID APPROVAL COMMAND
    // =====================================

    private static GameApprovalCommand NewCommand(Guid gameId)
    {
        return new GameApprovalCommand(
            GameId: gameId,
            AdminUserId: "admin-001",
            ApprovalRecordId: "approval-001",
            ApprovedAtUtc: ApprovedAtUtc,
            RewardEventId: Guid.NewGuid(),
            CorrelationId: Guid.NewGuid(),
            RewardRuleVersion: "game-creator-v1");
    }

    // =====================================
    // PREPARE SUBMITTED GAME
    // =====================================

    private static async Task<(
        InMemoryGameSubmissionStore Store,
        Game Game,
        GameApprovalCommand Command)> PrepareAsync()
    {
        var store = new InMemoryGameSubmissionStore();

        var game = NewDraft();

        store.AddDraft(game);

        var identity = new ApprovalIdentitySnapshotDto(
            CreatorId,
            "Thuvarathan",
            game.Id,
            SubmittedAtUtc);

        await store.SubmitDraftAndEnqueueAsync(
            identity);

        var command = NewCommand(game.Id);

        return (store, game, command);
    }

    // =====================================
    // TEST 01 - COMPLETE APPROVAL WORKFLOW
    // =====================================

    [Fact]
    public async Task Approval_ShouldPublishGameAndEnqueueReward()
    {
        var setup = await PrepareAsync();

        var service = new GameApprovalService(setup.Store);

        var reward = await service.ApproveAsync(setup.Command);

        Assert.Equal(
            GameStatus.Published,
            setup.Game.Status);

        Assert.Equal(
            ApprovedAtUtc,
            setup.Game.ApprovedAtUtc);

        Assert.Equal(
            ApprovedAtUtc.AddDays(30),
            setup.Game.ExpiresAtUtc);

        Assert.Equal(
            CoinRewardType.GameCreatorApproved,
            reward.RewardType);

        Assert.Equal(100, reward.CoinsAmount);

        Assert.Equal(
            CreatorId,
            reward.RecipientUserId);

        Assert.Equal(
            setup.Game.Id.ToString("D"),
            reward.SourceRecordId);

        Assert.Equal(
            setup.Command.RewardEventId,
            reward.EventId);

        Assert.Equal(
            setup.Command.ApprovalRecordId,
            reward.ApprovalRecordId);

        CoinRewardContractRules.ValidateGrant(reward);

        Assert.Empty(setup.Store.GetPendingItems());
        Assert.Single(setup.Store.GetCreatorRewardOutbox());
    }

    // =====================================
    // TEST 02 - EXPIRY AFTER 30 DAYS
    // =====================================

    [Fact]
    public async Task PublishedGame_ShouldExpireAfter30Days()
    {
        var setup = await PrepareAsync();

        var service = new GameApprovalService(setup.Store);

        await service.ApproveAsync(setup.Command);

        Assert.True(
            setup.Game.CanPlay(ApprovedAtUtc));

        Assert.True(
            setup.Game.CanPlay(
                ApprovedAtUtc.AddDays(30).AddTicks(-1)));

        Assert.False(
            setup.Game.CanPlay(
                ApprovedAtUtc.AddDays(30)));

        var archived = setup.Game.ArchiveIfExpired(
            ApprovedAtUtc.AddDays(30));

        Assert.True(archived);

        Assert.Equal(
            GameStatus.Expired,
            setup.Game.Status);

        // The reward event must not disappear.
        Assert.Single(
            setup.Store.GetCreatorRewardOutbox());
    }

    // =====================================
    // TEST 03 - IDENTICAL RETRY
    // =====================================

    [Fact]
    public async Task IdenticalRetry_ShouldNotDuplicateReward()
    {
        var setup = await PrepareAsync();

        var service = new GameApprovalService(setup.Store);

        var first = await service.ApproveAsync(
            setup.Command);

        var second = await service.ApproveAsync(
            setup.Command);

        Assert.Same(first, second);

        Assert.Single(
            setup.Store.GetCreatorRewardOutbox());

        Assert.Equal(
            GameStatus.Published,
            setup.Game.Status);
    }

    // =====================================
    // TEST 04 - CONFLICTING REWARD EVENT
    // =====================================

    [Fact]
    public async Task ConflictingRetry_ShouldBeRejected()
    {
        var setup = await PrepareAsync();

        var service = new GameApprovalService(setup.Store);

        await service.ApproveAsync(setup.Command);

        var conflicting = setup.Command with
        {
            RewardEventId = Guid.NewGuid()
        };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ApproveAsync(conflicting));

        Assert.Single(
            setup.Store.GetCreatorRewardOutbox());

        Assert.Equal(
            GameStatus.Published,
            setup.Game.Status);
    }

    // =====================================
    // TEST 05 - CONFLICTING APPROVAL TIME
    // =====================================

    [Fact]
    public async Task DifferentApprovalTime_ShouldBeRejected()
    {
        var setup = await PrepareAsync();

        var service = new GameApprovalService(setup.Store);

        await service.ApproveAsync(setup.Command);

        var conflicting = setup.Command with
        {
            ApprovedAtUtc = ApprovedAtUtc.AddMinutes(1)
        };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ApproveAsync(conflicting));

        Assert.Equal(
            ApprovedAtUtc,
            setup.Game.ApprovedAtUtc);

        Assert.Single(
            setup.Store.GetCreatorRewardOutbox());
    }

    // =====================================
    // TEST 06 - MISSING GAME
    // =====================================

    [Fact]
    public async Task MissingGame_ShouldNotCreateReward()
    {
        var store = new InMemoryGameSubmissionStore();

        var service = new GameApprovalService(store);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => service.ApproveAsync(
                NewCommand(Guid.NewGuid())));

        Assert.Empty(store.GetCreatorRewardOutbox());
    }

    // =====================================
    // TEST 07 - UNSUBMITTED DRAFT
    // =====================================

    [Fact]
    public async Task DraftGame_ShouldNotBeApproved()
    {
        var store = new InMemoryGameSubmissionStore();

        var game = NewDraft();

        store.AddDraft(game);

        var service = new GameApprovalService(store);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ApproveAsync(
                NewCommand(game.Id)));

        Assert.Equal(GameStatus.Draft, game.Status);

        Assert.Empty(store.GetCreatorRewardOutbox());
    }

    // =====================================
    // TEST 08 - APPROVAL BEFORE SUBMISSION
    // =====================================

    [Fact]
    public async Task EarlyApproval_ShouldNotChangeGame()
    {
        var setup = await PrepareAsync();

        var service = new GameApprovalService(setup.Store);

        var invalid = setup.Command with
        {
            ApprovedAtUtc =
                SubmittedAtUtc.AddMinutes(-1)
        };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ApproveAsync(invalid));

        Assert.Equal(
            GameStatus.PendingApproval,
            setup.Game.Status);

        Assert.Null(setup.Game.ApprovedAtUtc);

        Assert.Single(setup.Store.GetPendingItems());
        Assert.Empty(setup.Store.GetCreatorRewardOutbox());
    }

    // =====================================
    // TEST 09 - INVALID ADMIN REFERENCE
    // =====================================

    [Fact]
    public async Task MissingAdminId_ShouldBeRejected()
    {
        var setup = await PrepareAsync();

        var invalid = setup.Command with
        {
            AdminUserId = " "
        };

        // Test the store's own validation.
        await Assert.ThrowsAsync<ArgumentException>(
            () => setup.Store
                .ApproveAndEnqueueCreatorRewardAsync(invalid));

        Assert.Equal(
            GameStatus.PendingApproval,
            setup.Game.Status);

        Assert.Empty(setup.Store.GetCreatorRewardOutbox());
    }

    // =====================================
    // TEST 10 - NON UTC APPROVAL
    // =====================================

    [Fact]
    public async Task NonUtcApproval_ShouldBeRejected()
    {
        var setup = await PrepareAsync();

        var service = new GameApprovalService(setup.Store);

        var invalid = setup.Command with
        {
            ApprovedAtUtc = new DateTimeOffset(
                2026, 11, 14, 15, 30, 0,
                TimeSpan.FromMinutes(330))
        };

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.ApproveAsync(invalid));

        Assert.Equal(
            GameStatus.PendingApproval,
            setup.Game.Status);

        Assert.Empty(setup.Store.GetCreatorRewardOutbox());
    }

    // =====================================
    // TEST 11 - CANCELLED APPROVAL
    // =====================================

    [Fact]
    public async Task CancelledApproval_ShouldPreserveQueue()
    {
        var setup = await PrepareAsync();

        var service = new GameApprovalService(setup.Store);

        using var cancellation =
            new CancellationTokenSource();

        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.ApproveAsync(
                setup.Command,
                cancellation.Token));

        Assert.Equal(
            GameStatus.PendingApproval,
            setup.Game.Status);

        Assert.Single(setup.Store.GetPendingItems());

        Assert.Empty(setup.Store.GetCreatorRewardOutbox());
    }

    // =====================================
    // TEST 12 - CONCURRENT APPROVAL REQUESTS
    // =====================================

    [Fact]
    public async Task ConcurrentIdenticalApprovals_ShouldCreateOneReward()
    {
        var setup = await PrepareAsync();

        var service = new GameApprovalService(setup.Store);

        var requests = Enumerable.Range(0, 12)
            .Select(_ => Task.Run(
                () => service.ApproveAsync(setup.Command)))
            .ToArray();

        var results = await Task.WhenAll(requests);

        Assert.Equal(12, results.Length);

        Assert.All(results, reward =>
        {
            Assert.Same(results[0], reward);
        });

        Assert.Single(
            setup.Store.GetCreatorRewardOutbox());

        Assert.Empty(setup.Store.GetPendingItems());

        Assert.Equal(
            GameStatus.Published,
            setup.Game.Status);
    }
}
