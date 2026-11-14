
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.Games.Dtos;
using Wayhunter.Application.Modules.Games.Services;
using Wayhunter.Application.Modules.IdentitySocial.Dtos;
using Wayhunter.Domain.Modules.Games.Entities;
using Wayhunter.Domain.Modules.Games.Enums;
using Wayhunter.Infrastructure.Persistence.Repositories.Games;
using Xunit;

namespace Wayhunter.UnitTests.Modules.Games;

public sealed class GameModerationWorkflowTests
{
    private const string CreatorId = "creator-001";

    private static readonly DateTimeOffset SubmittedAt =
        new(2026, 11, 14, 6, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset ApprovedAt =
        SubmittedAt.AddHours(2);

    private static readonly DateTimeOffset ModeratedAt =
        ApprovedAt.AddHours(1);

    // =====================================
    // CREATE VALID GAME DRAFT
    // =====================================

    private static Game NewDraft()
    {
        var game = Game.CreateDraft(
            Guid.NewGuid(),
            CreatorId,
            "Jaffna Treasure Hunt",
            "Find hidden locations",
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
            CreatorId,
            new[] { step },
            new[] { hint });

        return game;
    }

    // =====================================
    // PREPARE PENDING GAME
    // =====================================

    private static async Task<(
        InMemoryGameSubmissionStore Store,
        Game Game)> PrepareAsync()
    {
        var store = new InMemoryGameSubmissionStore();
        var game = NewDraft();

        store.AddDraft(game);

        var identity = new ApprovalIdentitySnapshotDto(
            CreatorId,
            "Thuvarathan",
            game.Id,
            SubmittedAt);

        await store.SubmitDraftAndEnqueueAsync(identity);

        return (store, game);
    }

    // =====================================
    // MODERATION COMMAND
    // =====================================

    private static GameModerationCommand NewCommand(
        Guid gameId,
        GameModerationAction action,
        string reason = "Unsafe game content",
        DateTimeOffset? occurredAt = null)
    {
        return new GameModerationCommand(
            DecisionId: Guid.NewGuid(),
            GameId: gameId,
            AdminUserId: "admin-001",
            Action: action,
            Reason: reason,
            OccurredAtUtc: occurredAt ?? ModeratedAt,
            CorrelationId: Guid.NewGuid());
    }

    // =====================================
    // APPROVAL COMMAND
    // =====================================

    private static GameApprovalCommand NewApproval(
        Guid gameId)
    {
        return new GameApprovalCommand(
            GameId: gameId,
            AdminUserId: "admin-001",
            ApprovalRecordId: "approval-001",
            ApprovedAtUtc: ApprovedAt,
            RewardEventId: Guid.NewGuid(),
            CorrelationId: Guid.NewGuid(),
            RewardRuleVersion: "game-creator-v1");
    }

    // =====================================
    // TEST 01 - REJECT WORKFLOW
    // =====================================

    [Fact]
    public async Task Reject_ShouldRemoveQueueAndCreateAudit()
    {
        var setup = await PrepareAsync();

        var service =
            new GameModerationService(setup.Store);

        var command = NewCommand(
            setup.Game.Id,
            GameModerationAction.Reject,
            " Invalid location ");

        var audit = await service.ModerateAsync(command);

        Assert.Equal(
            GameStatus.Rejected,
            setup.Game.Status);

        Assert.Equal(
            "Invalid location",
            setup.Game.RejectionReason);

        Assert.Equal(
            GameModerationAction.Reject,
            audit.Action);

        Assert.Equal(
            GameStatus.Rejected,
            audit.ResultingStatus);

        Assert.Equal(
            command.DecisionId,
            audit.DecisionId);

        Assert.Empty(setup.Store.GetPendingItems());

        // Rejection must not create creator rewards.
        Assert.Empty(
            setup.Store.GetCreatorRewardOutbox());

        Assert.Single(
            setup.Store.GetModerationAuditRecords());

        Assert.False(
            setup.Game.CanPlay(ModeratedAt));
    }

    // =====================================
    // TEST 02 - FLAG PENDING GAME
    // =====================================

    [Fact]
    public async Task FlagPending_ShouldBlockApproval()
    {
        var setup = await PrepareAsync();

        var service =
            new GameModerationService(setup.Store);

        await service.ModerateAsync(NewCommand(
            setup.Game.Id,
            GameModerationAction.Flag));

        Assert.True(setup.Game.IsFlaggedForReview);

        Assert.Equal(
            GameStatus.PendingApproval,
            setup.Game.Status);

        Assert.Single(
            setup.Store.GetPendingItems());

        var approvalService =
            new GameApprovalService(setup.Store);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => approvalService.ApproveAsync(
                NewApproval(setup.Game.Id)));

        Assert.Null(setup.Game.ApprovedAtUtc);

        Assert.Empty(
            setup.Store.GetCreatorRewardOutbox());

        Assert.Single(
            setup.Store.GetModerationAuditRecords());
    }

    // =====================================
    // TEST 03 - FLAG PUBLISHED GAME
    // =====================================

    [Fact]
    public async Task FlagPublished_ShouldStopGameplay()
    {
        var setup = await PrepareAsync();

        var approvalService =
            new GameApprovalService(setup.Store);

        await approvalService.ApproveAsync(
            NewApproval(setup.Game.Id));

        Assert.True(setup.Game.CanPlay(ModeratedAt));

        var moderationService =
            new GameModerationService(setup.Store);

        var audit = await moderationService.ModerateAsync(
            NewCommand(
                setup.Game.Id,
                GameModerationAction.Flag));

        Assert.True(setup.Game.IsFlaggedForReview);

        Assert.False(setup.Game.CanPlay(ModeratedAt));

        Assert.Equal(
            GameStatus.Published,
            audit.ResultingStatus);

        // Previous reward event remains.
        Assert.Single(
            setup.Store.GetCreatorRewardOutbox());

        Assert.Single(
            setup.Store.GetModerationAuditRecords());
    }

    // =====================================
    // TEST 04 - DISABLE PUBLISHED GAME
    // =====================================

    [Fact]
    public async Task DisablePublished_ShouldStopGameplay()
    {
        var setup = await PrepareAsync();

        var approvalService =
            new GameApprovalService(setup.Store);

        await approvalService.ApproveAsync(
            NewApproval(setup.Game.Id));

        var moderationService =
            new GameModerationService(setup.Store);

        var audit = await moderationService.ModerateAsync(
            NewCommand(
                setup.Game.Id,
                GameModerationAction.Disable,
                "Dangerous game location"));

        Assert.Equal(
            GameStatus.Disabled,
            setup.Game.Status);

        Assert.Equal(
            "Dangerous game location",
            setup.Game.DisabledReason);

        Assert.Equal(
            GameStatus.Disabled,
            audit.ResultingStatus);

        Assert.False(
            setup.Game.CanPlay(ModeratedAt));

        Assert.Throws<InvalidOperationException>(
            () => setup.Game.ApproveVerifiedWinner(
                "player-001",
                ModeratedAt));

        Assert.Single(
            setup.Store.GetCreatorRewardOutbox());

        Assert.Single(
            setup.Store.GetModerationAuditRecords());
    }

    // =====================================
    // TEST 05 - IDEMPOTENT RETRY
    // =====================================

    [Fact]
    public async Task SameDecision_ShouldReturnExistingAudit()
    {
        var setup = await PrepareAsync();

        var service =
            new GameModerationService(setup.Store);

        var command = NewCommand(
            setup.Game.Id,
            GameModerationAction.Reject);

        var first = await service.ModerateAsync(command);
        var second = await service.ModerateAsync(command);

        Assert.Same(first, second);

        Assert.Single(
            setup.Store.GetModerationAuditRecords());

        Assert.Empty(
            setup.Store.GetCreatorRewardOutbox());
    }

    // =====================================
    // TEST 06 - CONFLICTING DECISION ID
    // =====================================

    [Fact]
    public async Task SameDecisionIdDifferentReason_ShouldReject()
    {
        var setup = await PrepareAsync();

        var service =
            new GameModerationService(setup.Store);

        var command = NewCommand(
            setup.Game.Id,
            GameModerationAction.Reject,
            "First reason");

        await service.ModerateAsync(command);

        var conflicting = command with
        {
            Reason = "Different reason"
        };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ModerateAsync(conflicting));

        Assert.Single(
            setup.Store.GetModerationAuditRecords());

        Assert.Equal(
            "First reason",
            setup.Game.RejectionReason);
    }

    // =====================================
    // TEST 07 - DUPLICATE REJECTION
    // =====================================

    [Fact]
    public async Task DifferentDecisionAfterReject_ShouldFail()
    {
        var setup = await PrepareAsync();

        var service =
            new GameModerationService(setup.Store);

        await service.ModerateAsync(NewCommand(
            setup.Game.Id,
            GameModerationAction.Reject));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ModerateAsync(NewCommand(
                setup.Game.Id,
                GameModerationAction.Reject)));

        Assert.Single(
            setup.Store.GetModerationAuditRecords());

        Assert.Equal(
            GameStatus.Rejected,
            setup.Game.Status);
    }

    // =====================================
    // TEST 08 - GAME NOT FOUND
    // =====================================

    [Fact]
    public async Task MissingGame_ShouldNotCreateAudit()
    {
        var store = new InMemoryGameSubmissionStore();

        var service = new GameModerationService(store);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => service.ModerateAsync(NewCommand(
                Guid.NewGuid(),
                GameModerationAction.Reject)));

        Assert.Empty(store.GetModerationAuditRecords());
    }

    // =====================================
    // TEST 09 - CANCELLATION
    // =====================================

    [Fact]
    public async Task CancelledRequest_ShouldNotModifyGame()
    {
        var setup = await PrepareAsync();

        var service =
            new GameModerationService(setup.Store);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.ModerateAsync(
                NewCommand(
                    setup.Game.Id,
                    GameModerationAction.Reject),
                cts.Token));

        Assert.Equal(
            GameStatus.PendingApproval,
            setup.Game.Status);

        Assert.Single(
            setup.Store.GetPendingItems());

        Assert.Empty(
            setup.Store.GetModerationAuditRecords());
    }

    // =====================================
    // TEST 10 - CONCURRENT RETRIES
    // =====================================

    [Fact]
    public async Task ConcurrentSameDecision_ShouldCreateOneAudit()
    {
        var setup = await PrepareAsync();

        var service =
            new GameModerationService(setup.Store);

        var command = NewCommand(
            setup.Game.Id,
            GameModerationAction.Reject);

        var tasks = Enumerable.Range(0, 12)
            .Select(_ => Task.Run(
                () => service.ModerateAsync(command)))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        Assert.Equal(12, results.Length);

        Assert.All(results, result =>
        {
            Assert.Same(results[0], result);
        });

        Assert.Single(
            setup.Store.GetModerationAuditRecords());

        Assert.Equal(
            GameStatus.Rejected,
            setup.Game.Status);
    }

    // =====================================
    // TEST 11 - AUDIT GAME FILTER
    // =====================================

    [Fact]
    public async Task AuditQuery_ShouldReturnOnlySelectedGame()
    {
        var store = new InMemoryGameSubmissionStore();

        var firstGame = NewDraft();
        var secondGame = NewDraft();

        store.AddDraft(firstGame);
        store.AddDraft(secondGame);

        await store.SubmitDraftAndEnqueueAsync(
            new ApprovalIdentitySnapshotDto(
                CreatorId,
                "Thuvarathan",
                firstGame.Id,
                SubmittedAt));

        await store.SubmitDraftAndEnqueueAsync(
            new ApprovalIdentitySnapshotDto(
                CreatorId,
                "Thuvarathan",
                secondGame.Id,
                SubmittedAt));

        var service = new GameModerationService(store);

        await service.ModerateAsync(NewCommand(
            firstGame.Id,
            GameModerationAction.Reject));

        await service.ModerateAsync(NewCommand(
            secondGame.Id,
            GameModerationAction.Reject));

        Assert.Equal(
            2,
            store.GetModerationAuditRecords().Count);

        var firstAudit =
            store.GetModerationAuditRecords(firstGame.Id);

        Assert.Single(firstAudit);

        Assert.Equal(
            firstGame.Id,
            firstAudit[0].GameId);
    }

    // =====================================
    // TEST 12 - DIRECT STORE VALIDATION
    // =====================================

    [Fact]
    public async Task InvalidReason_ShouldNotChangeStore()
    {
        var setup = await PrepareAsync();

        var command = NewCommand(
            setup.Game.Id,
            GameModerationAction.Reject,
            "   ");

        await Assert.ThrowsAsync<ArgumentException>(
            () => setup.Store.ApplyAsync(command));

        Assert.Equal(
            GameStatus.PendingApproval,
            setup.Game.Status);

        Assert.Single(
            setup.Store.GetPendingItems());

        Assert.Empty(
            setup.Store.GetModerationAuditRecords());

        Assert.Empty(
            setup.Store.GetCreatorRewardOutbox());
    }
}
