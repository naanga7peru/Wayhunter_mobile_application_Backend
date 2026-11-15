
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.Games.Dtos;
using Wayhunter.Application.Modules.Games.Services;
using Wayhunter.Application.Modules.IdentitySocial.Dtos;
using Wayhunter.Domain.Modules.Games.Entities;
using Wayhunter.Domain.Modules.Games.Enums;
using Wayhunter.Infrastructure.Persistence.Repositories.Games;
using Xunit;

namespace Wayhunter.UnitTests.Modules.Games;

public sealed class CurrentGameStageQueryTests
{
    // =====================================
    // TEST 1
    // RETURN CURRENT STAGE HINTS ONLY
    // =====================================

    [Fact]
    public async Task CurrentStage_ShouldNotExposeFutureHints()
    {
        // Arrange
        var (store, game, approvedAt) =
            await CreatePublishedGameAsync();

        var nowUtc = approvedAt.AddMinutes(5);

        var attempt = await store.StartOrResumeAsync(
            game.Id,
            "player-001",
            nowUtc);

        // Act
        var result = await store.GetCurrentStageAsync(
            game.Id,
            attempt.AttemptId,
            "player-001",
            nowUtc);

        // Assert
        Assert.Equal(
            attempt.AttemptId,
            result.AttemptId);

        Assert.Equal(game.Id, result.GameId);

        Assert.Equal(1, result.CurrentStepNumber);

        Assert.Equal(2, result.TotalSteps);

        Assert.Equal(
            GameProgressStatus.InProgress,
            result.ProgressStatus);

        Assert.Equal(
            attempt.DeadlineUtc,
            result.DeadlineUtc);

        Assert.Equal(
            game.DraftSteps[0].Id,
            result.GameStepId);

        // Only the first stage hint is public.
        var hint = Assert.Single(result.Hints);

        Assert.Equal(
            "Find the first bridge",
            hint.TextHint);

        Assert.Equal(
            "stage-1-photo",
            hint.PhotoMediaId);

        // Future stage must remain hidden.
        Assert.DoesNotContain(
            result.Hints,
            item => item.TextHint == "SECRET SECOND STAGE");

        Assert.DoesNotContain(
            result.Hints,
            item => item.PhotoMediaId == "secret-stage-2-photo");
    }

    // =====================================
    // TEST 2
    // ANOTHER PLAYER CANNOT READ ATTEMPT
    // =====================================

    [Fact]
    public async Task AnotherPlayer_ShouldNotAccessAttempt()
    {
        // Arrange
        var (store, game, approvedAt) =
            await CreatePublishedGameAsync();

        var nowUtc = approvedAt.AddMinutes(5);

        var attempt = await store.StartOrResumeAsync(
            game.Id,
            "player-001",
            nowUtc);

        // Act + Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => store.GetCurrentStageAsync(
                game.Id,
                attempt.AttemptId,
                "player-002",
                nowUtc));
    }

    // =====================================
    // TEST HELPER
    // CREATE TWO-STAGE PUBLISHED GAME
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

        // Create draft Game.
        var game = Game.CreateDraft(
            Guid.NewGuid(),
            "creator-001",
            "Jaffna Two Stage Treasure Hunt",
            "Find two hidden locations",
            60);

        // Stage 1.
        var step1 = new GameStep(
            Guid.NewGuid(),
            game.Id,
            1);

        // Stage 2.
        var step2 = new GameStep(
            Guid.NewGuid(),
            game.Id,
            2);

        // Public first-stage clue.
        var hint1 = new GameHint(
            Guid.NewGuid(),
            step1.Id,
            "Find the first bridge",
            "stage-1-photo");

        // Future stage clue - must be hidden.
        var hint2 = new GameHint(
            Guid.NewGuid(),
            step2.Id,
            "SECRET SECOND STAGE",
            "secret-stage-2-photo");

        game.ReplaceDraftStages(
            "creator-001",
            new[] { step1, step2 },
            new[] { hint1, hint2 });

        store.AddDraft(game);

        // Submit Game for Admin verification.
        await store.SubmitDraftAndEnqueueAsync(
            new ApprovalIdentitySnapshotDto(
                "creator-001",
                "Game Creator",
                game.Id,
                submittedAt));

        // Admin approves Game.
        var approvalService = new GameApprovalService(store);

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
    // TEST 3 - WRONG ATTEMPT ID
    // =====================================

    [Fact]
    public async Task WrongAttemptId_ShouldRejectStageQuery()
    {
        var (store, game, approvedAt) =
            await CreatePublishedGameAsync();

        var nowUtc = approvedAt.AddMinutes(5);

        // Start a valid attempt.
        var attempt = await store.StartOrResumeAsync(
            game.Id,
            "player-001",
            nowUtc);

        // Another attempt ID must not work.
        var wrongAttemptId = Guid.NewGuid();

        Assert.NotEqual(
            attempt.AttemptId,
            wrongAttemptId);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => store.GetCurrentStageAsync(
                game.Id,
                wrongAttemptId,
                "player-001",
                nowUtc));
    }

    // =====================================
    // TEST 4 - ATTEMPT DEADLINE EXPIRED
    // =====================================

    [Fact]
    public async Task ExpiredAttemptDeadline_ShouldRejectStageQuery()
    {
        var (store, game, approvedAt) =
            await CreatePublishedGameAsync();

        var startedAt = approvedAt.AddMinutes(5);

        var attempt = await store.StartOrResumeAsync(
            game.Id,
            "player-001",
            startedAt);

        // Exact attempt deadline boundary.
        var deadlineUtc = attempt.DeadlineUtc;

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.GetCurrentStageAsync(
                game.Id,
                attempt.AttemptId,
                "player-001",
                deadlineUtc));

        // After deadline must also be rejected.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.GetCurrentStageAsync(
                game.Id,
                attempt.AttemptId,
                "player-001",
                deadlineUtc.AddSeconds(1)));
    }

    // =====================================
    // TEST 5 - GAME EXPIRED
    // =====================================

    [Fact]
    public async Task ExpiredGame_ShouldRejectStageQuery()
    {
        var (store, game, approvedAt) =
            await CreatePublishedGameAsync();

        var startedAt = approvedAt.AddMinutes(5);

        var attempt = await store.StartOrResumeAsync(
            game.Id,
            "player-001",
            startedAt);

        // Game expires exactly 30 days
        // after Admin approval.
        var gameExpiry = game.ExpiresAtUtc!.Value;

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.GetCurrentStageAsync(
                game.Id,
                attempt.AttemptId,
                "player-001",
                gameExpiry));
    }

    // =====================================
    // TEST 6 - CANCELLED REQUEST
    // =====================================

    [Fact]
    public async Task CancelledRequest_ShouldRejectStageQuery()
    {
        var (store, game, approvedAt) =
            await CreatePublishedGameAsync();

        var nowUtc = approvedAt.AddMinutes(5);

        var attempt = await store.StartOrResumeAsync(
            game.Id,
            "player-001",
            nowUtc);

        using var cts =
            new System.Threading.CancellationTokenSource();

        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => store.GetCurrentStageAsync(
                game.Id,
                attempt.AttemptId,
                "player-001",
                nowUtc,
                cts.Token));
    }

    // =====================================
    // TEST 7 & 8
    // MODERATED GAME MUST HIDE CLUES
    // =====================================

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ModeratedGame_ShouldRejectStageQuery(
        bool flagForReview)
    {
        // Arrange
        var (store, game, approvedAt) =
            await CreatePublishedGameAsync();

        var startedAt = approvedAt.AddMinutes(5);

        // Start a valid attempt first.
        var attempt = await store.StartOrResumeAsync(
            game.Id,
            "player-001",
            startedAt);

        var moderationTime = startedAt.AddMinutes(1);

        // Admin moderation after attempt started.
        if (flagForReview)
        {
            game.FlagForReview(
                "Suspicious clues",
                moderationTime);
        }
        else
        {
            game.DisableGame(
                "Safety restriction",
                moderationTime);
        }

        // Player must not access clues anymore.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.GetCurrentStageAsync(
                game.Id,
                attempt.AttemptId,
                "player-001",
                moderationTime));
    }

    // =====================================
    // TEST 9
    // WINNER-CLOSED GAME MUST HIDE CLUES
    // =====================================

    [Fact]
    public async Task WinnerClosedGame_ShouldRejectStageQuery()
    {
        // Arrange
        var (store, game, approvedAt) =
            await CreatePublishedGameAsync();

        var startedAt = approvedAt.AddMinutes(5);

        var attempt = await store.StartOrResumeAsync(
            game.Id,
            "player-001",
            startedAt);

        var winnerApprovalTime =
            startedAt.AddMinutes(1);

        // Simulate an already verified winner.
        game.ApproveVerifiedWinner(
            "player-002",
            winnerApprovalTime);

        Assert.Equal(
            "player-002",
            game.WinnerUserId);

        // Existing player must not read clues
        // after a winner has been selected.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.GetCurrentStageAsync(
                game.Id,
                attempt.AttemptId,
                "player-001",
                winnerApprovalTime));
    }

}
