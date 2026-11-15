
using System;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.Games.Dtos;
using Wayhunter.Application.Modules.Games.Services;
using Wayhunter.Application.Modules.IdentitySocial.Dtos;
using Wayhunter.Domain.Modules.Games.Entities;
using Wayhunter.Infrastructure.Persistence.Repositories.Games;
using Xunit;

namespace Wayhunter.UnitTests.Modules.Games;

public sealed class StartGameAttemptTests
{
    // =====================================
    // TEST 1 - ELIGIBLE PLAYER START
    // =====================================

    [Fact]
    public async Task EligiblePlayer_ShouldStartGameAttempt()
    {
        // Arrange
        var (store, game, approvedAt) =
            await CreatePublishedGameAsync();

        var nowUtc = approvedAt.AddMinutes(5);

        // Act
        var result = await store.StartOrResumeAsync(
            game.Id,
            "  player-001  ",
            nowUtc);

        // Assert
        Assert.NotEqual(Guid.Empty, result.AttemptId);

        Assert.Equal(game.Id, result.GameId);

        Assert.Equal(
            "player-001",
            result.PlayerUserId);

        Assert.Equal(nowUtc, result.StartedAtUtc);

        // Game duration: 60 minutes.
        // Deadline must be server-calculated.
        Assert.Equal(
            nowUtc.AddMinutes(60),
            result.DeadlineUtc);

        Assert.Equal(1, result.CurrentStepNumber);

        Assert.False(result.ResumedExistingAttempt);
    }

    // =====================================
    // TEST 2 - REPEATED START
    // =====================================

    [Fact]
    public async Task RepeatedStart_ShouldResumeExistingAttempt()
    {
        // Arrange
        var (store, game, approvedAt) =
            await CreatePublishedGameAsync();

        var firstStartTime =
            approvedAt.AddMinutes(5);

        var secondStartTime =
            firstStartTime.AddMinutes(1);

        // Act
        var first = await store.StartOrResumeAsync(
            game.Id,
            "player-001",
            firstStartTime);

        var second = await store.StartOrResumeAsync(
            game.Id,
            "  player-001  ",
            secondStartTime);

        // Assert
        Assert.Equal(
            first.AttemptId,
            second.AttemptId);

        Assert.Equal(
            first.StartedAtUtc,
            second.StartedAtUtc);

        Assert.Equal(
            first.DeadlineUtc,
            second.DeadlineUtc);

        Assert.False(first.ResumedExistingAttempt);

        Assert.True(second.ResumedExistingAttempt);
    }

    // =====================================
    // TEST 3 - CREATOR CANNOT PLAY
    // =====================================

    [Fact]
    public async Task Creator_ShouldNotPlayOwnGame()
    {
        // Arrange
        var (store, game, approvedAt) =
            await CreatePublishedGameAsync();

        // Act + Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.StartOrResumeAsync(
                game.Id,
                "creator-001",
                approvedAt.AddMinutes(5)));
    }

    // =====================================
    // TEST HELPER
    // CREATE AND APPROVE GAME
    // =====================================

    private static async Task<(
        InMemoryGameSubmissionStore Store,
        Game Game,
        DateTimeOffset ApprovedAt)>
        CreatePublishedGameAsync()
    {
        var store = new InMemoryGameSubmissionStore();

        var submittedAt = new DateTimeOffset(
            2026, 11, 15, 8, 0, 0,
            TimeSpan.Zero);

        var approvedAt = submittedAt.AddMinutes(30);

        // Create draft.
        var game = Game.CreateDraft(
            Guid.NewGuid(),
            "creator-001",
            "Jaffna Treasure Game",
            "Find hidden places",
            60);

        // Add mandatory game step.
        var step = new GameStep(
            Guid.NewGuid(),
            game.Id,
            1);

        // Add mandatory clue.
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

        // Submit for Admin verification.
        await store.SubmitDraftAndEnqueueAsync(
            new ApprovalIdentitySnapshotDto(
                "creator-001",
                "Game Creator",
                game.Id,
                submittedAt));

        // Admin approves game.
        var approvalService =
            new GameApprovalService(store);

        await approvalService.ApproveAsync(
            new GameApprovalCommand(
                GameId: game.Id,
                AdminUserId: "admin-001",
                ApprovalRecordId:
                    $"approval-{game.Id:N}",
                ApprovedAtUtc: approvedAt,
                RewardEventId: Guid.NewGuid(),
                CorrelationId: Guid.NewGuid(),
                RewardRuleVersion: "game-creator-v1"));

        return (store, game, approvedAt);
    }

    // =====================================
    // TEST 4 - DEADLINE MUST NOT EXCEED
    // GAME EXPIRY
    // =====================================

    [Fact]
    public async Task StartNearExpiry_ShouldCapDeadlineAtGameExpiry()
    {
        // Arrange
        var (store, game, approvedAt) =
            await CreatePublishedGameAsync();

        // Game expires 30 days after Admin approval.
        var gameExpiry = game.ExpiresAtUtc!.Value;

        // Player starts when only 15 minutes remain.
        var nowUtc = gameExpiry.AddMinutes(-15);

        // Act
        var result = await store.StartOrResumeAsync(
            game.Id,
            "player-001",
            nowUtc);

        // Assert
        Assert.NotEqual(Guid.Empty, result.AttemptId);

        Assert.Equal(nowUtc, result.StartedAtUtc);

        // The Game duration is 60 minutes,
        // but only 15 minutes are remaining.
        Assert.Equal(
            gameExpiry,
            result.DeadlineUtc);

        Assert.Equal(
            TimeSpan.FromMinutes(15),
            result.DeadlineUtc - result.StartedAtUtc);

        Assert.False(result.ResumedExistingAttempt);
    }

    // =====================================
    // TEST 5 - EXPIRED GAME MUST NOT START
    // =====================================

    [Fact]
    public async Task ExpiredGame_ShouldRejectNewAttempt()
    {
        // Arrange
        var (store, game, approvedAt) =
            await CreatePublishedGameAsync();

        // Exact 30-day expiry boundary.
        var expiredAt = game.ExpiresAtUtc!.Value;

        // Act + Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.StartOrResumeAsync(
                game.Id,
                "player-001",
                expiredAt));

        // Also reject after expiry.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.StartOrResumeAsync(
                game.Id,
                "player-002",
                expiredAt.AddMinutes(1)));
    }

    // =====================================
    // TEST 6 & 7
    // FLAGGED / DISABLED GAME RESTRICTIONS
    // =====================================

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ModeratedGame_ShouldRejectNewAttempt(
        bool flagForReview)
    {
        // Arrange
        var (store, game, approvedAt) =
            await CreatePublishedGameAsync();

        var nowUtc = approvedAt.AddMinutes(5);

        // Simulate authorized moderation
        // through existing domain methods.
        if (flagForReview)
        {
            game.FlagForReview(
                "Suspicious game clues",
                nowUtc);
        }
        else
        {
            game.DisableGame(
                "Safety restriction",
                nowUtc);
        }

        // Act + Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.StartOrResumeAsync(
                game.Id,
                "player-001",
                nowUtc));
    }

    // =====================================
    // TEST 8
    // GAME WITH WINNER MUST NOT START
    // =====================================

    [Fact]
    public async Task WinnerClosedGame_ShouldRejectNewAttempt()
    {
        // Arrange
        var (store, game, approvedAt) =
            await CreatePublishedGameAsync();

        var nowUtc = approvedAt.AddMinutes(5);

        // Simulate an already verified winner.
        game.ApproveVerifiedWinner(
            "winner-001",
            nowUtc);

        Assert.Equal(
            "winner-001",
            game.WinnerUserId);

        // Act + Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.StartOrResumeAsync(
                game.Id,
                "player-002",
                nowUtc));
    }

    // =====================================
    // TEST 9 - INVALID UTC
    // =====================================

    [Fact]
    public async Task InvalidUtc_ShouldRejectStartAttempt()
    {
        var (store, game, approvedAt) =
            await CreatePublishedGameAsync();

        var nonUtc = new DateTimeOffset(
            2026, 11, 15, 14, 0, 0,
            TimeSpan.FromHours(5.5));

        await Assert.ThrowsAsync<ArgumentException>(
            () => store.StartOrResumeAsync(
                game.Id,
                "player-001",
                nonUtc));

        await Assert.ThrowsAsync<ArgumentException>(
            () => store.StartOrResumeAsync(
                game.Id,
                "player-001",
                default(DateTimeOffset)));
    }

    // =====================================
    // TEST 10 - CANCELLATION
    // =====================================

    [Fact]
    public async Task CancelledRequest_ShouldRejectStartAttempt()
    {
        var (store, game, approvedAt) =
            await CreatePublishedGameAsync();

        var nowUtc = approvedAt.AddMinutes(5);

        using var cts =
            new System.Threading.CancellationTokenSource();

        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => store.StartOrResumeAsync(
                game.Id,
                "player-001",
                nowUtc,
                cts.Token));
    }

    // =====================================
    // TEST 11 - CONCURRENT START
    // =====================================

    [Fact]
    public async Task ConcurrentStart_ShouldCreateOnlyOneAttempt()
    {
        var (store, game, approvedAt) =
            await CreatePublishedGameAsync();

        var nowUtc = approvedAt.AddMinutes(5);

        // Simulate 20 concurrent requests
        // from the same player.
        var tasks =
            new Task<StartGameAttemptResultDto>[20];

        for (int i = 0; i < tasks.Length; i++)
        {
            tasks[i] = Task.Run(() =>
                store.StartOrResumeAsync(
                    game.Id,
                    "player-001",
                    nowUtc));
        }

        var results = await Task.WhenAll(tasks);

        // All requests must use the same Attempt ID.
        var firstAttemptId = results[0].AttemptId;

        Assert.NotEqual(Guid.Empty, firstAttemptId);

        foreach (var result in results)
        {
            Assert.Equal(
                firstAttemptId,
                result.AttemptId);

            Assert.Equal(
                results[0].DeadlineUtc,
                result.DeadlineUtc);
        }

        // Exactly one request creates the attempt.
        // The other 19 must resume it.
        int newAttemptCount = 0;
        int resumedCount = 0;

        foreach (var result in results)
        {
            if (result.ResumedExistingAttempt)
                resumedCount++;
            else
                newAttemptCount++;
        }

        Assert.Equal(1, newAttemptCount);
        Assert.Equal(19, resumedCount);
    }


}
