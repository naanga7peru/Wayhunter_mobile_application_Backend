
using System;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.Games.Dtos;
using Wayhunter.Application.Modules.Games.Services;
using Wayhunter.Application.Modules.IdentitySocial.Dtos;
using Wayhunter.Domain.Modules.CoinsWallet.Enums;
using Wayhunter.Domain.Modules.Games.Entities;
using Wayhunter.Domain.Modules.Games.Enums;
using Wayhunter.Infrastructure.Persistence.Repositories.Games;
using Xunit;

namespace Wayhunter.UnitTests.Modules.Games;

public sealed class GameApprovalDecisionRaceTests
{
    private const string CreatorId = "creator-001";

    private static readonly DateTimeOffset SubmittedAt =
        new(2026, 11, 14, 6, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset DecisionAt =
        SubmittedAt.AddHours(1);

    [Fact]
    public async Task ConcurrentApproveAndReject_OnlyOneDecisionWins()
    {
        // Arrange
        var store = new InMemoryGameSubmissionStore();

        var game = Game.CreateDraft(
            Guid.NewGuid(),
            CreatorId,
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
            "Find the first clue",
            null);

        game.ReplaceDraftStages(
            CreatorId,
            new[] { step },
            new[] { hint });

        store.AddDraft(game);

        await store.SubmitDraftAndEnqueueAsync(
            new ApprovalIdentitySnapshotDto(
                CreatorId,
                "Thuvarathan",
                game.Id,
                SubmittedAt));

        Assert.Equal(
            GameStatus.PendingApproval,
            game.Status);

        Assert.Single(store.GetPendingItems());
        Assert.Empty(store.GetCreatorRewardOutbox());

        var approvalService =
            new GameApprovalService(store);

        var moderationService =
            new GameModerationService(store);

        var approvalCommand = new GameApprovalCommand(
            GameId: game.Id,
            AdminUserId: "admin-001",
            ApprovalRecordId: "approval-race-001",
            ApprovedAtUtc: DecisionAt,
            RewardEventId: Guid.NewGuid(),
            CorrelationId: Guid.NewGuid(),
            RewardRuleVersion: "game-creator-v1");

        var rejectionCommand = new GameModerationCommand(
            DecisionId: Guid.NewGuid(),
            GameId: game.Id,
            AdminUserId: "admin-002",
            Action: GameModerationAction.Reject,
            Reason: "Unsafe game location",
            OccurredAtUtc: DecisionAt,
            CorrelationId: Guid.NewGuid());

        // Start both Admin decisions together.
        var start = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var approveTask = Task.Run(async () =>
        {
            await start.Task;

            try
            {
                await approvalService.ApproveAsync(
                    approvalCommand);

                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        });

        var rejectTask = Task.Run(async () =>
        {
            await start.Task;

            try
            {
                await moderationService.ModerateAsync(
                    rejectionCommand);

                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        });

        start.SetResult(true);

        await Task.WhenAll(approveTask, rejectTask);

        bool approved = await approveTask;
        bool rejected = await rejectTask;

        // Exactly one decision must succeed.
        Assert.NotEqual(approved, rejected);

        Assert.Empty(store.GetPendingItems());

        if (approved)
        {
            Assert.Equal(
                GameStatus.Published,
                game.Status);

            var reward = Assert.Single(
                store.GetCreatorRewardOutbox());

            Assert.Equal(
                CoinRewardType.GameCreatorApproved,
                reward.RewardType);

            Assert.Equal(100, reward.CoinsAmount);

            Assert.Equal(
                CreatorId,
                reward.RecipientUserId);

            Assert.Empty(
                store.GetModerationAuditRecords());
        }
        else
        {
            Assert.True(rejected);

            Assert.Equal(
                GameStatus.Rejected,
                game.Status);

            Assert.Equal(
                "Unsafe game location",
                game.RejectionReason);

            // Rejected game must receive zero rewards.
            Assert.Empty(
                store.GetCreatorRewardOutbox());

            var audit = Assert.Single(
                store.GetModerationAuditRecords());

            Assert.Equal(
                GameModerationAction.Reject,
                audit.Action);

            Assert.False(game.CanPlay(DecisionAt));
        }
    }

    // =====================================
    // M5-MS03-T07
    // REJECTED GAME MUST NEVER RECEIVE
    // CREATOR APPROVAL REWARD
    // =====================================

    [Fact]
    public async Task RejectedGame_CannotBeApprovedOrRewarded()
    {
        // Arrange - Create valid game.
        var store = new InMemoryGameSubmissionStore();

        var game = Game.CreateDraft(
            Guid.NewGuid(),
            CreatorId,
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
            "Find the first clue",
            null);

        game.ReplaceDraftStages(
            CreatorId,
            new[] { step },
            new[] { hint });

        store.AddDraft(game);

        // Submit to Admin.
        await store.SubmitDraftAndEnqueueAsync(
            new ApprovalIdentitySnapshotDto(
                CreatorId,
                "Thuvarathan",
                game.Id,
                SubmittedAt));

        // Pending game must have no reward.
        Assert.Equal(
            GameStatus.PendingApproval,
            game.Status);

        Assert.Empty(
            store.GetCreatorRewardOutbox());

        // Admin 1 rejects the game.
        var moderationService =
            new GameModerationService(store);

        var rejection = new GameModerationCommand(
            DecisionId: Guid.NewGuid(),
            GameId: game.Id,
            AdminUserId: "admin-001",
            Action: GameModerationAction.Reject,
            Reason: "Unsafe game location",
            OccurredAtUtc: DecisionAt,
            CorrelationId: Guid.NewGuid());

        var audit = await moderationService
            .ModerateAsync(rejection);

        Assert.Equal(
            GameStatus.Rejected,
            game.Status);

        Assert.Equal(
            "Unsafe game location",
            game.RejectionReason);

        Assert.Empty(store.GetPendingItems());

        Assert.Empty(
            store.GetCreatorRewardOutbox());

        // Admin 2 tries to approve rejected game.
        var approvalService =
            new GameApprovalService(store);

        var approval = new GameApprovalCommand(
            GameId: game.Id,
            AdminUserId: "admin-002",
            ApprovalRecordId: "approval-after-reject",
            ApprovedAtUtc: DecisionAt.AddMinutes(1),
            RewardEventId: Guid.NewGuid(),
            CorrelationId: Guid.NewGuid(),
            RewardRuleVersion: "game-creator-v1");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => approvalService.ApproveAsync(approval));

        // Rejected state cannot be bypassed.
        Assert.Equal(
            GameStatus.Rejected,
            game.Status);

        Assert.Null(game.ApprovedAtUtc);

        Assert.Null(game.ExpiresAtUtc);

        // No creator reward event may exist.
        Assert.Empty(
            store.GetCreatorRewardOutbox());

        // Rejection must retain exactly one audit.
        Assert.Same(
            audit,
            Assert.Single(
                store.GetModerationAuditRecords()));

        // Same rejection retry must be idempotent.
        var retry = await moderationService
            .ModerateAsync(rejection);

        Assert.Same(audit, retry);

        Assert.Single(
            store.GetModerationAuditRecords());

        Assert.Empty(
            store.GetCreatorRewardOutbox());
    }

    // =====================================
    // M5-MS03-T07
    // APPROVED GAME CANNOT BE REJECTED
    // CREATOR REWARD MUST NOT DUPLICATE
    // =====================================

    [Fact]
    public async Task ApprovedGame_CannotBeRejectedOrRewardedTwice()
    {
        // Arrange - Create a valid Game draft.
        var store = new InMemoryGameSubmissionStore();

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

        store.AddDraft(game);

        // Submit Game to Admin review.
        await store.SubmitDraftAndEnqueueAsync(
            new ApprovalIdentitySnapshotDto(
                CreatorId,
                "Thuvarathan",
                game.Id,
                SubmittedAt));

        // Pending Game has zero rewards.
        Assert.Equal(
            GameStatus.PendingApproval,
            game.Status);

        Assert.Empty(
            store.GetCreatorRewardOutbox());

        var approvalService =
            new GameApprovalService(store);

        var approvalCommand = new GameApprovalCommand(
            GameId: game.Id,
            AdminUserId: "admin-001",
            ApprovalRecordId: "approval-001",
            ApprovedAtUtc: DecisionAt,
            RewardEventId: Guid.NewGuid(),
            CorrelationId: Guid.NewGuid(),
            RewardRuleVersion: "game-creator-v1");

        // Admin 1 approves the Game.
        var firstReward = await approvalService
            .ApproveAsync(approvalCommand);

        Assert.Equal(
            GameStatus.Published,
            game.Status);

        Assert.Equal(
            100,
            firstReward.CoinsAmount);

        Assert.Equal(
            CoinRewardType.GameCreatorApproved,
            firstReward.RewardType);

        Assert.Equal(
            CreatorId,
            firstReward.RecipientUserId);

        Assert.Equal(
            game.Id.ToString("D"),
            firstReward.SourceRecordId);

        Assert.Empty(store.GetPendingItems());

        Assert.Single(
            store.GetCreatorRewardOutbox());

        // Admin 2 attempts to reject the approved Game.
        var moderationService =
            new GameModerationService(store);

        var rejectionCommand = new GameModerationCommand(
            DecisionId: Guid.NewGuid(),
            GameId: game.Id,
            AdminUserId: "admin-002",
            Action: GameModerationAction.Reject,
            Reason: "Late rejection attempt",
            OccurredAtUtc: DecisionAt.AddHours(1),
            CorrelationId: Guid.NewGuid());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => moderationService.ModerateAsync(
                rejectionCommand));

        // Published Game remains unchanged.
        Assert.Equal(
            GameStatus.Published,
            game.Status);

        Assert.Null(game.RejectionReason);

        Assert.Empty(
            store.GetModerationAuditRecords());

        // Retry using the identical approval command.
        var retryReward = await approvalService
            .ApproveAsync(approvalCommand);

        Assert.Same(
            firstReward,
            retryReward);

        // Conflicting new approval must fail.
        var conflictingApproval = approvalCommand with
        {
            RewardEventId = Guid.NewGuid()
        };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => approvalService.ApproveAsync(
                conflictingApproval));

        // Exactly one creator reward event.
        var rewards =
            store.GetCreatorRewardOutbox();

        var onlyReward = Assert.Single(rewards);

        Assert.Same(
            firstReward,
            onlyReward);

        Assert.Equal(
            100,
            onlyReward.CoinsAmount);

        Assert.Equal(
            GameStatus.Published,
            game.Status);

        Assert.Equal(
            DecisionAt.AddDays(30),
            game.ExpiresAtUtc);
    }

    // =====================================
    // M5-MS03-T07
    // PENDING / REJECTED / APPROVED
    // CREATOR REWARD ISOLATION
    // =====================================

    [Fact]
    public async Task OnlyApprovedGame_ShouldCreateCreatorReward()
    {
        var store = new InMemoryGameSubmissionStore();

        // Create and submit a valid Game.
        async Task<Game> SubmitGameAsync(string title)
        {
            var game = Game.CreateDraft(
                Guid.NewGuid(),
                CreatorId,
                title,
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

            store.AddDraft(game);

            await store.SubmitDraftAndEnqueueAsync(
                new ApprovalIdentitySnapshotDto(
                    CreatorId,
                    "Thuvarathan",
                    game.Id,
                    SubmittedAt));

            return game;
        }

        // Three different Games.
        var pendingGame = await SubmitGameAsync(
            "Pending Game");

        var rejectedGame = await SubmitGameAsync(
            "Rejected Game");

        var approvedGame = await SubmitGameAsync(
            "Approved Game");

        // All start pending with zero reward events.
        Assert.Equal(
            3,
            store.GetPendingItems().Count);

        Assert.Empty(
            store.GetCreatorRewardOutbox());

        // Reject Game B.
        var moderationService =
            new GameModerationService(store);

        await moderationService.ModerateAsync(
            new GameModerationCommand(
                DecisionId: Guid.NewGuid(),
                GameId: rejectedGame.Id,
                AdminUserId: "admin-001",
                Action: GameModerationAction.Reject,
                Reason: "Invalid game location",
                OccurredAtUtc: DecisionAt,
                CorrelationId: Guid.NewGuid()));

        Assert.Empty(
            store.GetCreatorRewardOutbox());

        // Approve Game C.
        var approvalService =
            new GameApprovalService(store);

        var reward = await approvalService.ApproveAsync(
            new GameApprovalCommand(
                GameId: approvedGame.Id,
                AdminUserId: "admin-001",
                ApprovalRecordId: "approval-isolation-001",
                ApprovedAtUtc: DecisionAt,
                RewardEventId: Guid.NewGuid(),
                CorrelationId: Guid.NewGuid(),
                RewardRuleVersion: "game-creator-v1"));

        // Verify all three Game states.
        Assert.Equal(
            GameStatus.PendingApproval,
            pendingGame.Status);

        Assert.Equal(
            GameStatus.Rejected,
            rejectedGame.Status);

        Assert.Equal(
            GameStatus.Published,
            approvedGame.Status);

        // Only Game A remains pending.
        var pendingItem = Assert.Single(
            store.GetPendingItems());

        Assert.Equal(
            pendingGame.Id,
            pendingItem.GameId);

        // Exactly one reward event exists.
        var onlyReward = Assert.Single(
            store.GetCreatorRewardOutbox());

        Assert.Same(reward, onlyReward);

        Assert.Equal(
            CoinRewardType.GameCreatorApproved,
            onlyReward.RewardType);

        Assert.Equal(
            100,
            onlyReward.CoinsAmount);

        Assert.Equal(
            CreatorId,
            onlyReward.RecipientUserId);

        // Reward belongs ONLY to approved Game C.
        Assert.Equal(
            approvedGame.Id.ToString("D"),
            onlyReward.SourceRecordId);

        Assert.NotEqual(
            pendingGame.Id.ToString("D"),
            onlyReward.SourceRecordId);

        Assert.NotEqual(
            rejectedGame.Id.ToString("D"),
            onlyReward.SourceRecordId);

        // Rejection audit must belong to Game B.
        var audit = Assert.Single(
            store.GetModerationAuditRecords());

        Assert.Equal(
            rejectedGame.Id,
            audit.GameId);

        Assert.Equal(
            GameModerationAction.Reject,
            audit.Action);

        // No published state or expiry for A and B.
        Assert.Null(pendingGame.ApprovedAtUtc);
        Assert.Null(rejectedGame.ApprovedAtUtc);

        Assert.Null(pendingGame.ExpiresAtUtc);
        Assert.Null(rejectedGame.ExpiresAtUtc);

        // Approved Game has correct expiry.
        Assert.Equal(
            DecisionAt.AddDays(30),
            approvedGame.ExpiresAtUtc);
    }


}
