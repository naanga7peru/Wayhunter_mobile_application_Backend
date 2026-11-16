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

/// <summary>M5-MS07-T01 — in-memory PendingAdmin completion verification.</summary>
public sealed class GameCompletionResultTests
{
    private const string PlayerId = "player-001";
    private const string CreatorId = "creator-001";
    private const string ReviewerId = "admin-001";

    [Fact]
    public async Task FinalApproval_CreatesPendingAdminWithApprovedProofReferences()
    {
        var s = await CreateScenarioAsync(stageCount: 2);
        var first = await SubmitAsync(s, 1, s.Now);
        await ApproveAsync(s, first, s.Now.AddMinutes(1));
        var final = await SubmitAsync(s, 2, s.Now.AddMinutes(2));
        var decision = await ApproveAsync(s, final, s.Now.AddMinutes(3));

        Assert.Equal(GameProgressStatus.Completed, decision.ProgressStatus);
        var completion = s.Store.GetCompletionResult(s.Attempt.AttemptId);
        Assert.NotNull(completion);
        Assert.Equal(GameCompletionResultStatus.PendingAdmin, completion.Status);
        Assert.Equal(s.Game.Id, completion.GameId);
        Assert.Equal(s.Attempt.AttemptId, completion.AttemptId);
        Assert.Equal(PlayerId, completion.PlayerUserId);
        Assert.Equal(new[] { first.Id, final.Id }, completion.ApprovedStageEvidenceIds);
        Assert.Equal(s.Now.AddMinutes(3), completion.CompletedAtUtc);
        Assert.True(completion.AttemptStartedAtUtc <= completion.CompletedAtUtc);
        Assert.True(completion.CompletedAtUtc < completion.AttemptDeadlineUtc);
        Assert.Single(s.Store.GetCompletionResults());
        Assert.Equal(PlayerId, s.Game.WinnerUserId);
    }

    [Fact]
    public async Task IncompleteStages_DoNotCreateCompletion()
    {
        var s = await CreateScenarioAsync(stageCount: 2);
        var first = await SubmitAsync(s, 1, s.Now);
        var decision = await ApproveAsync(s, first, s.Now.AddMinutes(1));

        Assert.Equal(GameProgressStatus.InProgress, decision.ProgressStatus);
        Assert.Null(s.Store.GetCompletionResult(s.Attempt.AttemptId));
        Assert.Empty(s.Store.GetCompletionResults());
        Assert.Null(s.Game.WinnerUserId);
    }

    [Fact]
    public async Task ExpiredAttempt_RejectsFinalApprovalWithoutCompletionOrWinner()
    {
        var s = await CreateScenarioAsync(stageCount: 1);
        var proof = await SubmitAsync(s, 1, s.Now);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ApproveAsync(s, proof, s.Now.AddMinutes(61)));

        Assert.Null(s.Store.GetCompletionResult(s.Attempt.AttemptId));
        Assert.Empty(s.Store.GetCompletionResults());
        Assert.Null(s.Game.WinnerUserId);
    }

    [Fact]
    public async Task DuplicateFinalDecision_DoesNotCreateSecondCompletion()
    {
        var s = await CreateScenarioAsync(stageCount: 1);
        var proof = await SubmitAsync(s, 1, s.Now);
        var command = Decision(s, proof);

        var first = await s.Store.ApplyDecisionAsync(
            command, ReviewerId, s.Now.AddMinutes(1));
        var completion = s.Store.GetCompletionResult(s.Attempt.AttemptId);
        Assert.NotNull(completion);

        var replay = await s.Store.ApplyDecisionAsync(
            command, ReviewerId, s.Now.AddMinutes(2));
        Assert.Equal(first, replay);
        Assert.Same(completion, s.Store.GetCompletionResult(s.Attempt.AttemptId));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            s.Store.ApplyDecisionAsync(
                Decision(s, proof), ReviewerId, s.Now.AddMinutes(2)));
        Assert.Single(s.Store.GetCompletionResults());
    }

    [Fact]
    public async Task CreatorCannotStartOwnGame_AndNoCompletionIsCreated()
    {
        var s = await CreateScenarioAsync(stageCount: 1);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            s.Store.StartOrResumeAsync(s.Game.Id, CreatorId, s.Now));

        Assert.Empty(s.Store.GetCompletionResults());
        Assert.Null(s.Game.WinnerUserId);
    }

    private static GameStageProofDecisionCommand Decision(
        Scenario s, GameEvidence evidence) => new(
            Guid.NewGuid(), s.Game.Id, s.Attempt.AttemptId,
            evidence.Id, evidence.StepNumber, true);

    private static Task<GameStageProofDecisionResultDto> ApproveAsync(
        Scenario s, GameEvidence evidence, DateTimeOffset reviewedAtUtc) =>
        s.Store.ApplyDecisionAsync(
            Decision(s, evidence), ReviewerId, reviewedAtUtc);

    private static Task<GameEvidence> SubmitAsync(
        Scenario s, int step, DateTimeOffset submittedAtUtc) =>
        s.Store.SubmitCurrentStageProofAsync(
            new SubmitGameStageProofCommand(
                s.Game.Id, s.Attempt.AttemptId, step,
                $"camera-photo-{Guid.NewGuid():N}",
                $"camera-session-{Guid.NewGuid():N}"),
            PlayerId, submittedAtUtc);

    private static async Task<Scenario> CreateScenarioAsync(int stageCount)
    {
        var store = new InMemoryGameSubmissionStore(
            new FakeCaptureVerifier(), new FakeReviewerAuthorizer());
        var submittedAt = new DateTimeOffset(
            2026, 11, 15, 8, 0, 0, TimeSpan.Zero);
        var approvedAt = submittedAt.AddMinutes(30);
        var now = approvedAt.AddMinutes(5);

        var game = Game.CreateDraft(
            Guid.NewGuid(), CreatorId, "Jaffna Treasure Hunt",
            "Completion result test", 60);
        var steps = Enumerable.Range(1, stageCount)
            .Select(n => new GameStep(Guid.NewGuid(), game.Id, n))
            .ToArray();
        var hints = steps.Select(step => new GameHint(
            Guid.NewGuid(), step.Id,
            $"Hint {step.StepNumber}",
            $"hint-photo-{step.StepNumber}"))
            .ToArray();

        game.ReplaceDraftStages(CreatorId, steps, hints);
        store.AddDraft(game);
        await store.SubmitDraftAndEnqueueAsync(
            new ApprovalIdentitySnapshotDto(
                CreatorId, "Game Creator", game.Id, submittedAt));
        await new GameApprovalService(store).ApproveAsync(
            new GameApprovalCommand(
                GameId: game.Id,
                AdminUserId: ReviewerId,
                ApprovalRecordId: $"approval-{game.Id:N}",
                ApprovedAtUtc: approvedAt,
                RewardEventId: Guid.NewGuid(),
                CorrelationId: Guid.NewGuid(),
                RewardRuleVersion: "game-creator-v1"));

        var attempt = await store.StartOrResumeAsync(game.Id, PlayerId, now);
        return new Scenario(store, game, attempt, now);
    }

    private sealed record Scenario(
        InMemoryGameSubmissionStore Store,
        Game Game,
        StartGameAttemptResultDto Attempt,
        DateTimeOffset Now);

    private sealed class FakeReviewerAuthorizer : IGameStageProofDecisionAuthorizer
    {
        public Task<bool> IsAuthorizedAsync(
            string authenticatedReviewerUserId,
            Guid gameId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(authenticatedReviewerUserId == ReviewerId);
        }
    }

    private sealed class FakeCaptureVerifier : IGameProofCaptureVerifier
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
