
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.Games.Dtos;
using Wayhunter.Application.Modules.Games.Interfaces;
using Wayhunter.Application.Modules.Games.Services;
using Wayhunter.Application.Modules.IdentitySocial.Dtos;
using Wayhunter.Domain.Modules.Games.Entities;
using Wayhunter.Infrastructure.Persistence.Repositories.Games;
using Xunit;

namespace Wayhunter.UnitTests.Modules.Games;

/// <summary>
/// M5 Game pending stage-proof review queue tests.
/// </summary>
public sealed class GamePendingStageProofQueryTests
{
    private const string CreatorId = "creator-001";
    private const string PlayerId = "player-001";
    private const string ReviewerId = "admin-001";

    [Fact]
    public async Task PendingProof_ShouldAppearInReviewQueue()
    {
        var s = await CreateScenarioAsync();

        var evidence = await SubmitAsync(s);

        var queue = await s.Store.GetPendingStageProofsAsync();

        var item = Assert.Single(queue);

        Assert.Equal(s.Game.Id, item.GameId);
        Assert.Equal(s.Game.Title, item.GameTitle);
        Assert.Equal(PlayerId, item.PlayerUserId);
        Assert.Equal(s.Attempt.AttemptId, item.AttemptId);
        Assert.Equal(evidence.Id, item.EvidenceId);
        Assert.Equal(1, item.StepNumber);
        Assert.Equal(evidence.MediaId, item.MediaId);
        Assert.Equal(s.Now, item.SubmittedAtUtc);
        Assert.Equal(s.Attempt.StartedAtUtc,
            item.AttemptStartedAtUtc);
        Assert.Equal(s.Attempt.DeadlineUtc,
            item.AttemptDeadlineUtc);
    }

    [Fact]
    public async Task NoPendingProofs_ShouldReturnEmptyQueue()
    {
        var s = await CreateScenarioAsync();

        var queue = await s.Store.GetPendingStageProofsAsync();

        Assert.Empty(queue);
    }

    [Fact]
    public async Task ApprovedProof_ShouldDisappearFromQueue()
    {
        var s = await CreateScenarioAsync();
        var evidence = await SubmitAsync(s);

        Assert.Single(
            await s.Store.GetPendingStageProofsAsync());

        await ApproveAsync(s, evidence);

        var queue = await s.Store.GetPendingStageProofsAsync();

        Assert.Empty(queue);
    }

    [Fact]
    public async Task RejectedProof_ShouldDisappearFromQueue()
    {
        var s = await CreateScenarioAsync();
        var evidence = await SubmitAsync(s);

        await RejectAsync(s, evidence);

        var queue = await s.Store.GetPendingStageProofsAsync();

        Assert.Empty(queue);
    }

    [Fact]
    public async Task RejectedOldProof_ShouldNotReappearAfterRetry()
    {
        var s = await CreateScenarioAsync();

        var firstEvidence = await SubmitAsync(s);

        await RejectAsync(s, firstEvidence);

        var retryEvidence = await SubmitAsync(
            s,
            s.Now.AddMinutes(2));

        var queue = await s.Store.GetPendingStageProofsAsync();

        var item = Assert.Single(queue);

        Assert.Equal(retryEvidence.Id, item.EvidenceId);
        Assert.NotEqual(firstEvidence.Id, item.EvidenceId);
    }

    [Fact]
    public async Task MultiplePlayers_ShouldAppearAsSeparateQueueItems()
    {
        var s = await CreateScenarioAsync();

        var firstEvidence = await SubmitAsync(s);

        const string secondPlayerId = "player-002";

        var secondAttempt = await s.Store.StartOrResumeAsync(
            s.Game.Id,
            secondPlayerId,
            s.Now);

        var secondEvidence =
            await s.Store.SubmitCurrentStageProofAsync(
                new SubmitGameStageProofCommand(
                    s.Game.Id,
                    secondAttempt.AttemptId,
                    1,
                    "second-player-photo",
                    "second-player-session"),
                secondPlayerId,
                s.Now);

        var queue = await s.Store.GetPendingStageProofsAsync();

        Assert.Equal(2, queue.Count);

        Assert.Contains(queue, item =>
            item.EvidenceId == firstEvidence.Id &&
            item.PlayerUserId == PlayerId);

        Assert.Contains(queue, item =>
            item.EvidenceId == secondEvidence.Id &&
            item.PlayerUserId == secondPlayerId);
    }

    [Fact]
    public async Task CancelledQuery_ShouldThrowOperationCanceledException()
    {
        var s = await CreateScenarioAsync();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => s.Store.GetPendingStageProofsAsync(
                cts.Token));
    }

    // =========================================
    // SHARED HELPERS
    // =========================================

    private static Task<GameEvidence> SubmitAsync(
        Scenario s,
        DateTimeOffset? submittedAtUtc = null)
    {
        var command = new SubmitGameStageProofCommand(
            s.Game.Id,
            s.Attempt.AttemptId,
            1,
            $"media-{Guid.NewGuid():N}",
            $"session-{Guid.NewGuid():N}");

        return s.Store.SubmitCurrentStageProofAsync(
            command,
            PlayerId,
            submittedAtUtc ?? s.Now);
    }

    private static async Task ApproveAsync(
        Scenario s,
        GameEvidence evidence)
    {
        await s.Store.ApplyDecisionAsync(
            new GameStageProofDecisionCommand(
                Guid.NewGuid(),
                s.Game.Id,
                s.Attempt.AttemptId,
                evidence.Id,
                evidence.StepNumber,
                true),
            ReviewerId,
            s.Now.AddMinutes(1));
    }

    private static async Task RejectAsync(
        Scenario s,
        GameEvidence evidence)
    {
        await s.Store.ApplyDecisionAsync(
            new GameStageProofDecisionCommand(
                Guid.NewGuid(),
                s.Game.Id,
                s.Attempt.AttemptId,
                evidence.Id,
                evidence.StepNumber,
                false,
                "Proof is invalid"),
            ReviewerId,
            s.Now.AddMinutes(1));
    }

    private static async Task<Scenario> CreateScenarioAsync()
    {
        var verifier = new FakeCaptureVerifier();
        var authorizer = new FakeReviewerAuthorizer();

        var store = new InMemoryGameSubmissionStore(
            verifier,
            authorizer);

        var submittedAt = new DateTimeOffset(
            2026, 11, 15, 8, 0, 0, TimeSpan.Zero);

        var approvedAt = submittedAt.AddMinutes(30);
        var now = approvedAt.AddMinutes(5);

        var game = Game.CreateDraft(
            Guid.NewGuid(),
            CreatorId,
            "Jaffna Treasure Hunt",
            "Pending proof query test",
            60);

        var steps = Enumerable.Range(1, 2)
            .Select(number => new GameStep(
                Guid.NewGuid(),
                game.Id,
                number))
            .ToArray();

        var hints = steps
            .Select(step => new GameHint(
                Guid.NewGuid(),
                step.Id,
                $"Hint {step.StepNumber}",
                $"photo-{step.StepNumber}"))
            .ToArray();

        game.ReplaceDraftStages(
            CreatorId,
            steps,
            hints);

        store.AddDraft(game);

        await store.SubmitDraftAndEnqueueAsync(
            new ApprovalIdentitySnapshotDto(
                CreatorId,
                "Game Creator",
                game.Id,
                submittedAt));

        await new GameApprovalService(store)
            .ApproveAsync(new GameApprovalCommand(
                GameId: game.Id,
                AdminUserId: ReviewerId,
                ApprovalRecordId: $"approval-{game.Id:N}",
                ApprovedAtUtc: approvedAt,
                RewardEventId: Guid.NewGuid(),
                CorrelationId: Guid.NewGuid(),
                RewardRuleVersion: "game-creator-v1"));

        var attempt = await store.StartOrResumeAsync(
            game.Id,
            PlayerId,
            now);

        return new Scenario(
            store,
            game,
            attempt,
            now);
    }

    private sealed record Scenario(
        InMemoryGameSubmissionStore Store,
        Game Game,
        StartGameAttemptResultDto Attempt,
        DateTimeOffset Now);

    private sealed class FakeReviewerAuthorizer
        : IGameStageProofDecisionAuthorizer
    {
        public Task<bool> IsAuthorizedAsync(
            string authenticatedReviewerUserId,
            Guid gameId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(
                authenticatedReviewerUserId == ReviewerId);
        }
    }

    private sealed class FakeCaptureVerifier
        : IGameProofCaptureVerifier
    {
        public Task<bool> VerifyAndConsumeAsync(
            string captureSessionId,
            string mediaId,
            Guid gameId,
            Guid attemptId,
            int stepNumber,
            string authenticatedPlayerUserId,
            DateTimeOffset nowUtc,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(true);
        }
    }
}
