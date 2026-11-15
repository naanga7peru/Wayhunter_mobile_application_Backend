
using System;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.Games.Dtos;
using Wayhunter.Application.Modules.Games.Interfaces;
using Wayhunter.Application.Modules.Games.Services;
using Wayhunter.Application.Modules.IdentitySocial.Dtos;
using Wayhunter.Domain.Modules.Games.Entities;
using Wayhunter.Domain.Modules.Games.Enums;
using Wayhunter.Infrastructure.Persistence.Repositories.Games;
using Xunit;

namespace Wayhunter.UnitTests.Modules.Games;

public sealed class AvailableGamesQueryTests
{
    [Fact]
    public async Task OnlyApprovedGame_ShouldAppearInAvailableList()
    {
        // Arrange
        var store = new InMemoryGameSubmissionStore();

        var submittedAt = new DateTimeOffset(
            2026, 11, 15, 8, 0, 0, TimeSpan.Zero);

        var approvedAt = submittedAt.AddMinutes(30);
        var nowUtc = approvedAt.AddMinutes(30);

        // Game A - will be approved.
        var approvedGame = await SubmitGameAsync(
            store,
            "Jaffna Treasure Hunt",
            submittedAt);

        // Game B - stays pending.
        var pendingGame = await SubmitGameAsync(
            store,
            "Pending Treasure Hunt",
            submittedAt);

        var approvalService = new GameApprovalService(store);

        await approvalService.ApproveAsync(
            new GameApprovalCommand(
                GameId: approvedGame.Id,
                AdminUserId: "admin-001",
                ApprovalRecordId: "approval-001",
                ApprovedAtUtc: approvedAt,
                RewardEventId: Guid.NewGuid(),
                CorrelationId: Guid.NewGuid(),
                RewardRuleVersion: "game-creator-v1"));

        // Act - read available games.
        IAvailableGamesQueryStore query = store;

        var results = await query.GetAvailableGamesAsync(
            nowUtc);

        // Assert - only approved Game A is public.
        var available = Assert.Single(results);

        Assert.Equal(approvedGame.Id, available.GameId);

        Assert.Equal(
            "Jaffna Treasure Hunt",
            available.Title);

        Assert.Equal(60, available.DurationMinutes);

        Assert.Equal(
            approvedAt,
            available.PublishedAtUtc);

        Assert.Equal(
            approvedAt.AddDays(30),
            available.ExpiresAtUtc);

        // Pending Game B must not appear.
        Assert.Equal(
            GameStatus.PendingApproval,
            pendingGame.Status);

        Assert.DoesNotContain(
            results,
            game => game.GameId == pendingGame.Id);

        Assert.Equal(
            GameStatus.Published,
            approvedGame.Status);
    }

    private static async Task<Game> SubmitGameAsync(
        InMemoryGameSubmissionStore store,
        string title,
        DateTimeOffset submittedAt)
    {
        var game = Game.CreateDraft(
            Guid.NewGuid(),
            "creator-001",
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
            "creator-001",
            new[] { step },
            new[] { hint });

        store.AddDraft(game);

        await store.SubmitDraftAndEnqueueAsync(
            new ApprovalIdentitySnapshotDto(
                "creator-001",
                "Thuvarathan",
                game.Id,
                submittedAt));

        return game;
    }

    // =====================================
    // M5-MS04-T01
    // REJECTED / FLAGGED / DISABLED
    // GAMES MUST NOT BE PUBLIC
    // =====================================

    [Fact]
    public async Task RejectedFlaggedDisabledGames_ShouldBeHidden()
    {
        // Arrange
        var store = new InMemoryGameSubmissionStore();

        var submittedAt = new DateTimeOffset(
            2026, 11, 15, 8, 0, 0, TimeSpan.Zero);

        var approvedAt = submittedAt.AddMinutes(30);
        var nowUtc = approvedAt.AddHours(1);

        // Four separate games.
        var activeGame = await SubmitGameAsync(
            store, "Active Game", submittedAt);

        var rejectedGame = await SubmitGameAsync(
            store, "Rejected Game", submittedAt);

        var flaggedGame = await SubmitGameAsync(
            store, "Flagged Game", submittedAt);

        var disabledGame = await SubmitGameAsync(
            store, "Disabled Game", submittedAt);

        var approvalService = new GameApprovalService(store);
        var moderationService = new GameModerationService(store);

        // Approve three games.
        foreach (var game in new[]
        {
            activeGame,
            flaggedGame,
            disabledGame
        })
        {
            await approvalService.ApproveAsync(
                new GameApprovalCommand(
                    GameId: game.Id,
                    AdminUserId: "admin-001",
                    ApprovalRecordId: $"approval-{game.Id:N}",
                    ApprovedAtUtc: approvedAt,
                    RewardEventId: Guid.NewGuid(),
                    CorrelationId: Guid.NewGuid(),
                    RewardRuleVersion: "game-creator-v1"));
        }

        // Reject the pending game.
        await moderationService.ModerateAsync(
            new GameModerationCommand(
                DecisionId: Guid.NewGuid(),
                GameId: rejectedGame.Id,
                AdminUserId: "admin-001",
                Action: GameModerationAction.Reject,
                Reason: "Unsafe location",
                OccurredAtUtc: approvedAt,
                CorrelationId: Guid.NewGuid()));

        // Flag the published game.
        await moderationService.ModerateAsync(
            new GameModerationCommand(
                DecisionId: Guid.NewGuid(),
                GameId: flaggedGame.Id,
                AdminUserId: "admin-001",
                Action: GameModerationAction.Flag,
                Reason: "Suspicious clues",
                OccurredAtUtc: approvedAt.AddMinutes(10),
                CorrelationId: Guid.NewGuid()));

        // Disable another published game.
        await moderationService.ModerateAsync(
            new GameModerationCommand(
                DecisionId: Guid.NewGuid(),
                GameId: disabledGame.Id,
                AdminUserId: "admin-001",
                Action: GameModerationAction.Disable,
                Reason: "Safety restriction",
                OccurredAtUtc: approvedAt.AddMinutes(10),
                CorrelationId: Guid.NewGuid()));

        // Act
        var results = await store.GetAvailableGamesAsync(nowUtc);

        // Assert - only the active game should appear.
        var available = Assert.Single(results);

        Assert.Equal(activeGame.Id, available.GameId);

        Assert.Equal(GameStatus.Rejected, rejectedGame.Status);
        Assert.True(flaggedGame.IsFlaggedForReview);
        Assert.Equal(GameStatus.Disabled, disabledGame.Status);

        Assert.DoesNotContain(
            results, item => item.GameId == rejectedGame.Id);

        Assert.DoesNotContain(
            results, item => item.GameId == flaggedGame.Id);

        Assert.DoesNotContain(
            results, item => item.GameId == disabledGame.Id);

        // Three moderation actions must be audited.
        Assert.Equal(
            3,
            store.GetModerationAuditRecords().Count);
    }

    // =====================================
    // M5-MS04-T01
    // EXPIRED AND WINNER-CLOSED GAME FILTERS
    // =====================================

    [Fact]
    public async Task ExpiredAndWinnerClosedGames_ShouldBeHidden()
    {
        // Arrange
        var store = new InMemoryGameSubmissionStore();

        var submittedAt = new DateTimeOffset(
            2026, 11, 15, 8, 0, 0, TimeSpan.Zero);

        var approvedAt = submittedAt.AddMinutes(30);
        var nowUtc = approvedAt.AddHours(1);

        // Game A - Active and playable.
        var activeGame = await SubmitGameAsync(
            store, "Active Treasure Hunt", submittedAt);

        // Game B - Expires exactly at nowUtc.
        var expiredSubmittedAt =
            nowUtc.AddDays(-30).AddMinutes(-30);

        var expiredApprovedAt =
            expiredSubmittedAt.AddMinutes(30);

        var expiredGame = await SubmitGameAsync(
            store, "Expired Treasure Hunt",
            expiredSubmittedAt);

        // Game C - Has a verified winner.
        var winnerGame = await SubmitGameAsync(
            store, "Completed Treasure Hunt", submittedAt);

        var approvalService = new GameApprovalService(store);

        // Local helper for Admin approval.
        async Task ApproveAsync(
            Game game,
            DateTimeOffset approvalTime)
        {
            await approvalService.ApproveAsync(
                new GameApprovalCommand(
                    GameId: game.Id,
                    AdminUserId: "admin-001",
                    ApprovalRecordId:
                        $"approval-{game.Id:N}",
                    ApprovedAtUtc: approvalTime,
                    RewardEventId: Guid.NewGuid(),
                    CorrelationId: Guid.NewGuid(),
                    RewardRuleVersion: "game-creator-v1"));
        }

        // Approve all three games.
        await ApproveAsync(activeGame, approvedAt);

        await ApproveAsync(
            expiredGame, expiredApprovedAt);

        await ApproveAsync(winnerGame, approvedAt);

        // Select one verified winner for Game C.
        winnerGame.ApproveVerifiedWinner(
            "player-002",
            nowUtc);

        // Verify initial states.
        Assert.Equal(
            GameStatus.Published,
            activeGame.Status);

        Assert.Equal(
            GameStatus.Published,
            expiredGame.Status);

        Assert.Equal(
            "player-002",
            winnerGame.WinnerUserId);

        // Exact 30-day expiry boundary.
        Assert.Equal(
            nowUtc,
            expiredGame.ExpiresAtUtc);

        // Act - before running the archive operation.
        var results = await store.GetAvailableGamesAsync(
            nowUtc);

        // Assert - only active game is visible.
        var available = Assert.Single(results);

        Assert.Equal(activeGame.Id, available.GameId);

        Assert.DoesNotContain(
            results,
            item => item.GameId == expiredGame.Id);

        Assert.DoesNotContain(
            results,
            item => item.GameId == winnerGame.Id);

        Assert.False(expiredGame.CanPlay(nowUtc));
        Assert.False(winnerGame.CanPlay(nowUtc));
        Assert.True(activeGame.CanPlay(nowUtc));

        // Archive the expired Game.
        Assert.True(
            expiredGame.ArchiveIfExpired(nowUtc));

        Assert.Equal(
            GameStatus.Expired,
            expiredGame.Status);

        // Archived Game must remain hidden.
        var afterArchive =
            await store.GetAvailableGamesAsync(nowUtc);

        Assert.Equal(
            activeGame.Id,
            Assert.Single(afterArchive).GameId);
    }

    // =====================================
    // M5-MS04-T01
    // SERVER UTC VALIDATION
    // =====================================

    [Fact]
    public async Task AvailableGames_InvalidUtc_ShouldBeRejected()
    {
        var store = new InMemoryGameSubmissionStore();

        var nonUtc = new DateTimeOffset(
            2026, 11, 15, 10, 0, 0,
            TimeSpan.FromHours(5.5));

        await Assert.ThrowsAsync<ArgumentException>(
            () => store.GetAvailableGamesAsync(nonUtc));

        await Assert.ThrowsAsync<ArgumentException>(
            () => store.GetAvailableGamesAsync(default(DateTimeOffset)));
    }

    // =====================================
    // M5-MS04-T01
    // CANCELLATION SAFETY
    // =====================================

    [Fact]
    public async Task AvailableGames_CancelledRequest_ShouldBeRejected()
    {
        var store = new InMemoryGameSubmissionStore();

        var nowUtc = new DateTimeOffset(
            2026, 11, 15, 8, 0, 0,
            TimeSpan.Zero);

        using var cts =
            new System.Threading.CancellationTokenSource();

        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => store.GetAvailableGamesAsync(
                nowUtc,
                cts.Token));
    }

    // =====================================
    // M5-MS04-T01
    // DETERMINISTIC ORDER
    // =====================================

    [Fact]
    public async Task AvailableGames_ShouldBeOrderedByApprovalTime()
    {
        var store = new InMemoryGameSubmissionStore();

        var submittedAt = new DateTimeOffset(
            2026, 11, 15, 8, 0, 0,
            TimeSpan.Zero);

        var earlyApproval = submittedAt.AddMinutes(10);
        var lateApproval = submittedAt.AddMinutes(20);
        var nowUtc = submittedAt.AddHours(1);

        // Create the later-approved game first.
        var lateGame = await SubmitGameAsync(
            store,
            "Late Approved Game",
            submittedAt);

        var earlyGame = await SubmitGameAsync(
            store,
            "Early Approved Game",
            submittedAt);

        var approvalService =
            new GameApprovalService(store);

        // Approve late game first.
        await approvalService.ApproveAsync(
            new GameApprovalCommand(
                GameId: lateGame.Id,
                AdminUserId: "admin-001",
                ApprovalRecordId:
                    $"approval-{lateGame.Id:N}",
                ApprovedAtUtc: lateApproval,
                RewardEventId: Guid.NewGuid(),
                CorrelationId: Guid.NewGuid(),
                RewardRuleVersion: "game-creator-v1"));

        // Approve early game second.
        await approvalService.ApproveAsync(
            new GameApprovalCommand(
                GameId: earlyGame.Id,
                AdminUserId: "admin-001",
                ApprovalRecordId:
                    $"approval-{earlyGame.Id:N}",
                ApprovedAtUtc: earlyApproval,
                RewardEventId: Guid.NewGuid(),
                CorrelationId: Guid.NewGuid(),
                RewardRuleVersion: "game-creator-v1"));

        // Act
        var results =
            await store.GetAvailableGamesAsync(nowUtc);

        // Assert
        Assert.Equal(2, results.Count);

        Assert.Equal(
            earlyGame.Id,
            results[0].GameId);

        Assert.Equal(
            lateGame.Id,
            results[1].GameId);

        Assert.Equal(
            earlyApproval,
            results[0].PublishedAtUtc);

        Assert.Equal(
            lateApproval,
            results[1].PublishedAtUtc);
    }


}
