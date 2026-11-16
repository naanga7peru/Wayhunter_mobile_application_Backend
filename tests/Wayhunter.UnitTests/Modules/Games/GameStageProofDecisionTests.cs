
using System;
using System.Linq;
using System.Threading;
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

/// <summary>
/// M5-MS06-T05
/// Game proof decision security and stage advancement tests.
/// </summary>
public sealed class GameStageProofDecisionTests
{
    private const string PlayerId = "player-001";
    private const string CreatorId = "creator-001";
    private const string ReviewerId = "admin-001";

    [Fact]
    public async Task ApprovedProof_ShouldUnlockNextStage()
    {
        var s = await CreateScenarioAsync();
        var evidence = await SubmitAsync(s);

        var result = await s.Store.ApplyDecisionAsync(
            Decision(s, evidence, approved: true),
            ReviewerId,
            s.Now.AddMinutes(1));

        Assert.True(result.Approved);
        Assert.Equal(2, result.CurrentStepNumber);
        Assert.Equal(GameProgressStatus.InProgress,
            result.ProgressStatus);

        var stage = await s.Store.GetCurrentStageAsync(
            s.Game.Id, s.Attempt.AttemptId,
            PlayerId, s.Now.AddMinutes(2));

        Assert.Equal(2, stage.CurrentStepNumber);
        Assert.Contains(stage.Hints,
            hint => hint.TextHint == "Hint 2");
        Assert.DoesNotContain(stage.Hints,
            hint => hint.TextHint == "Hint 1");
    }

    [Fact]
    public async Task RejectedProof_ShouldAllowRetry()
    {
        var s = await CreateScenarioAsync();
        var evidence = await SubmitAsync(s);

        var result = await s.Store.ApplyDecisionAsync(
            Decision(s, evidence, approved: false),
            ReviewerId,
            s.Now.AddMinutes(1));

        Assert.False(result.Approved);
        Assert.Equal(1, result.CurrentStepNumber);
        Assert.Equal(1, result.RejectedEvidenceCount);
        Assert.Equal(GameProgressStatus.InProgress,
            result.ProgressStatus);

        // A fresh proof can be submitted after rejection.
        var retry = await SubmitAsync(
            s, step: 1, atUtc: s.Now.AddMinutes(2));

        Assert.NotEqual(evidence.Id, retry.Id);
    }

    [Fact]
    public async Task ThirdRejection_ShouldCloseAttempt()
    {
        var s = await CreateScenarioAsync();

        GameStageProofDecisionResultDto? last = null;

        for (int i = 0; i < 3; i++)
        {
            var at = s.Now.AddMinutes(i * 2);
            var evidence = await SubmitAsync(s, atUtc: at);

            last = await s.Store.ApplyDecisionAsync(
                Decision(s, evidence, approved: false),
                ReviewerId,
                at.AddMinutes(1));
        }

        Assert.NotNull(last);
        Assert.Equal(3, last.RejectedEvidenceCount);
        Assert.Equal(GameProgressStatus.Rejected,
            last.ProgressStatus);
        Assert.Equal(1, last.CurrentStepNumber);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => SubmitAsync(
                s, atUtc: s.Now.AddMinutes(7)));
    }

    [Fact]
    public async Task UnauthorizedReviewer_ShouldBeRejected()
    {
        var s = await CreateScenarioAsync();
        var evidence = await SubmitAsync(s);

        s.Authorizer.Allow = false;

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => s.Store.ApplyDecisionAsync(
                Decision(s, evidence, approved: true),
                ReviewerId,
                s.Now.AddMinutes(1)));

        var stage = await s.Store.GetCurrentStageAsync(
            s.Game.Id, s.Attempt.AttemptId,
            PlayerId, s.Now.AddMinutes(2));

        Assert.Equal(1, stage.CurrentStepNumber);
        Assert.Equal(
            GameProgressStatus.WaitingForVerification,
            stage.ProgressStatus);
    }

    [Fact]
    public async Task MissingAuthorizer_ShouldFailClosed()
    {
        var s = await CreateScenarioAsync(
            includeAuthorizer: false);

        var evidence = await SubmitAsync(s);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => s.Store.ApplyDecisionAsync(
                Decision(s, evidence, approved: true),
                ReviewerId,
                s.Now.AddMinutes(1)));
    }

    [Fact]
    public async Task UnknownEvidence_ShouldNotAdvance()
    {
        var s = await CreateScenarioAsync();
        await SubmitAsync(s);

        var command = new GameStageProofDecisionCommand(
            Guid.NewGuid(),
            s.Game.Id,
            s.Attempt.AttemptId,
            Guid.NewGuid(),
            1,
            true);

        await Assert.ThrowsAsync<System.Collections.Generic
            .KeyNotFoundException>(
            () => s.Store.ApplyDecisionAsync(
                command, ReviewerId,
                s.Now.AddMinutes(1)));

        var stage = await s.Store.GetCurrentStageAsync(
            s.Game.Id, s.Attempt.AttemptId,
            PlayerId, s.Now.AddMinutes(2));

        Assert.Equal(1, stage.CurrentStepNumber);
    }

    [Fact]
    public async Task WrongStep_ShouldNotAdvance()
    {
        var s = await CreateScenarioAsync();
        var evidence = await SubmitAsync(s);

        var command = Decision(
            s, evidence, approved: true) with
        {
            StepNumber = 2
        };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => s.Store.ApplyDecisionAsync(
                command, ReviewerId,
                s.Now.AddMinutes(1)));
    }

    [Fact]
    public async Task IdenticalDecisionRetry_ShouldBeIdempotent()
    {
        var s = await CreateScenarioAsync();
        var evidence = await SubmitAsync(s);

        var command = Decision(
            s, evidence, approved: true);

        var first = await s.Store.ApplyDecisionAsync(
            command, ReviewerId,
            s.Now.AddMinutes(1));

        var second = await s.Store.ApplyDecisionAsync(
            command, ReviewerId,
            s.Now.AddMinutes(2));

        Assert.Equal(first, second);
        Assert.Equal(2, second.CurrentStepNumber);
    }

    [Fact]
    public async Task ConflictingDecisionId_ShouldBeRejected()
    {
        var s = await CreateScenarioAsync();
        var evidence = await SubmitAsync(s);

        var command = Decision(
            s, evidence, approved: true);

        await s.Store.ApplyDecisionAsync(
            command, ReviewerId,
            s.Now.AddMinutes(1));

        var conflict = command with
        {
            Approved = false,
            Reason = "Invalid photo"
        };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => s.Store.ApplyDecisionAsync(
                conflict, ReviewerId,
                s.Now.AddMinutes(2)));
    }

    [Fact]
    public async Task EvidenceCannotReceiveSecondDecision()
    {
        var s = await CreateScenarioAsync();
        var evidence = await SubmitAsync(s);

        await s.Store.ApplyDecisionAsync(
            Decision(s, evidence, approved: true),
            ReviewerId,
            s.Now.AddMinutes(1));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => s.Store.ApplyDecisionAsync(
                Decision(s, evidence, approved: true),
                ReviewerId,
                s.Now.AddMinutes(2)));
    }

    [Fact]
    public async Task FinalStage_ShouldCompleteAndSetWinner()
    {
        var s = await CreateScenarioAsync(stageCount: 1);
        var evidence = await SubmitAsync(s);

        var result = await s.Store.ApplyDecisionAsync(
            Decision(s, evidence, approved: true),
            ReviewerId,
            s.Now.AddMinutes(1));

        Assert.Equal(GameProgressStatus.Completed,
            result.ProgressStatus);

        Assert.Equal(PlayerId, s.Game.WinnerUserId);
        Assert.False(s.Game.CanPlay(
            s.Now.AddMinutes(2)));
    }

    [Fact]
    public async Task AnotherPendingPlayer_CannotBecomeSecondWinner()
    {
        var s = await CreateScenarioAsync(stageCount: 1);

        var otherAttempt = await s.Store.StartOrResumeAsync(
            s.Game.Id, "player-002", s.Now);

        var otherEvidence =
            await s.Store.SubmitCurrentStageProofAsync(
                new SubmitGameStageProofCommand(
                    s.Game.Id,
                    otherAttempt.AttemptId,
                    1,
                    "other-player-photo",
                    "other-player-session"),
                "player-002",
                s.Now);

        var firstEvidence = await SubmitAsync(s);

        await s.Store.ApplyDecisionAsync(
            Decision(s, firstEvidence, approved: true),
            ReviewerId,
            s.Now.AddMinutes(1));

        var secondDecision =
            new GameStageProofDecisionCommand(
                Guid.NewGuid(),
                s.Game.Id,
                otherAttempt.AttemptId,
                otherEvidence.Id,
                1,
                true);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => s.Store.ApplyDecisionAsync(
                secondDecision,
                ReviewerId,
                s.Now.AddMinutes(2)));

        Assert.Equal(PlayerId, s.Game.WinnerUserId);
    }

    // =========================================
    // SHARED TEST HELPERS
    // =========================================

    private static GameStageProofDecisionCommand Decision(
        Scenario s,
        GameEvidence evidence,
        bool approved)
    {
        return new GameStageProofDecisionCommand(
            Guid.NewGuid(),
            s.Game.Id,
            s.Attempt.AttemptId,
            evidence.Id,
            evidence.StepNumber,
            approved,
            approved ? null : "Proof is not valid");
    }

    private static Task<GameEvidence> SubmitAsync(
        Scenario s,
        int step = 1,
        DateTimeOffset? atUtc = null)
    {
        var command = new SubmitGameStageProofCommand(
            s.Game.Id,
            s.Attempt.AttemptId,
            step,
            $"camera-photo-{Guid.NewGuid():N}",
            $"camera-session-{Guid.NewGuid():N}");

        return s.Store.SubmitCurrentStageProofAsync(
            command, PlayerId, atUtc ?? s.Now);
    }

    private static async Task<Scenario> CreateScenarioAsync(
        bool includeAuthorizer = true,
        int stageCount = 2)
    {
        var authorizer = new FakeReviewerAuthorizer();
        var verifier = new FakeCaptureVerifier();

        var store = includeAuthorizer
            ? new InMemoryGameSubmissionStore(
                verifier, authorizer)
            : new InMemoryGameSubmissionStore(verifier);

        var submittedAt = new DateTimeOffset(
            2026, 11, 15, 8, 0, 0, TimeSpan.Zero);

        var approvedAt = submittedAt.AddMinutes(30);
        var now = approvedAt.AddMinutes(5);

        var game = Game.CreateDraft(
            Guid.NewGuid(),
            CreatorId,
            "Jaffna Treasure Hunt",
            "Stage proof decision test",
            60);

        var steps = Enumerable.Range(1, stageCount)
            .Select(number => new GameStep(
                Guid.NewGuid(), game.Id, number))
            .ToArray();

        var hints = steps
            .Select(step => new GameHint(
                Guid.NewGuid(),
                step.Id,
                $"Hint {step.StepNumber}",
                $"hint-photo-{step.StepNumber}"))
            .ToArray();

        game.ReplaceDraftStages(
            CreatorId, steps, hints);

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
                ApprovalRecordId:
                    $"approval-{game.Id:N}",
                ApprovedAtUtc: approvedAt,
                RewardEventId: Guid.NewGuid(),
                CorrelationId: Guid.NewGuid(),
                RewardRuleVersion: "game-creator-v1"));

        var attempt = await store.StartOrResumeAsync(
            game.Id, PlayerId, now);

        return new Scenario(
            store, game, attempt, now, authorizer);
    }

    private sealed record Scenario(
        InMemoryGameSubmissionStore Store,
        Game Game,
        StartGameAttemptResultDto Attempt,
        DateTimeOffset Now,
        FakeReviewerAuthorizer Authorizer);

    private sealed class FakeReviewerAuthorizer
        : IGameStageProofDecisionAuthorizer
    {
        public bool Allow { get; set; } = true;

        public Task<bool> IsAuthorizedAsync(
            string authenticatedReviewerUserId,
            Guid gameId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(
                Allow &&
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
