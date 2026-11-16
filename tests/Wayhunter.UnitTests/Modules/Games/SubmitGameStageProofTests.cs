
using System;
using System.Collections.Generic;
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

public sealed class SubmitGameStageProofTests
{
    private const string PlayerId = "player-001";
    private const string CreatorId = "creator-001";

    private static SubmitGameStageProofCommand Command(
        Game game,
        Guid attemptId,
        int stepNumber = 1,
        string mediaId = "camera-photo-001",
        string sessionId = "camera-session-001")
    {
        return new SubmitGameStageProofCommand(
            game.Id,
            attemptId,
            stepNumber,
            mediaId,
            sessionId);
    }

    // TEST 1: Valid proof moves to waiting state.
    [Fact]
    public async Task ValidProof_ShouldWaitForVerification()
    {
        var s = await CreateScenarioAsync();

        var evidence =
            await s.Store.SubmitCurrentStageProofAsync(
                Command(s.Game, s.Attempt.AttemptId),
                PlayerId,
                s.Now);

        Assert.NotEqual(Guid.Empty, evidence.Id);
        Assert.Equal(s.Attempt.AttemptId,
            evidence.AttemptId);
        Assert.Equal(1, evidence.StepNumber);
        Assert.Equal("camera-photo-001",
            evidence.MediaId);
        Assert.Equal(s.Now, evidence.SubmittedAtUtc);

        var stage = await s.Store.GetCurrentStageAsync(
            s.Game.Id,
            s.Attempt.AttemptId,
            PlayerId,
            s.Now);

        Assert.Equal(
            GameProgressStatus.WaitingForVerification,
            stage.ProgressStatus);

        Assert.Equal(1, stage.CurrentStepNumber);
        Assert.Equal(1, s.Verifier.CallCount);
    }

    // TEST 2: Unverified capture must be rejected.
    [Fact]
    public async Task InvalidCameraCapture_ShouldRejectProof()
    {
        var s = await CreateScenarioAsync();
        s.Verifier.Allow = false;

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => s.Store.SubmitCurrentStageProofAsync(
                Command(s.Game, s.Attempt.AttemptId),
                PlayerId,
                s.Now));

        var stage = await s.Store.GetCurrentStageAsync(
            s.Game.Id,
            s.Attempt.AttemptId,
            PlayerId,
            s.Now);

        Assert.Equal(GameProgressStatus.InProgress,
            stage.ProgressStatus);
        Assert.Equal(1, s.Verifier.CallCount);
    }

    // TEST 3: Repeated proof cannot be submitted
    // while Admin verification is pending.
    [Fact]
    public async Task DuplicateProof_ShouldBeRejected()
    {
        var s = await CreateScenarioAsync();
        var command = Command(
            s.Game, s.Attempt.AttemptId);

        await s.Store.SubmitCurrentStageProofAsync(
            command, PlayerId, s.Now);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => s.Store.SubmitCurrentStageProofAsync(
                command, PlayerId, s.Now));

        Assert.Equal(1, s.Verifier.CallCount);
    }

    // TEST 4: Future stage submissions are blocked.
    [Fact]
    public async Task FutureStage_ShouldBeRejected()
    {
        var s = await CreateScenarioAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => s.Store.SubmitCurrentStageProofAsync(
                Command(
                    s.Game,
                    s.Attempt.AttemptId,
                    stepNumber: 2),
                PlayerId,
                s.Now));

        Assert.Equal(0, s.Verifier.CallCount);
    }

    // TEST 5: Another player cannot submit proof.
    [Fact]
    public async Task AnotherPlayer_ShouldBeRejected()
    {
        var s = await CreateScenarioAsync();

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => s.Store.SubmitCurrentStageProofAsync(
                Command(s.Game, s.Attempt.AttemptId),
                "player-002",
                s.Now));

        Assert.Equal(0, s.Verifier.CallCount);
    }

    // TEST 6: Wrong Attempt ID cannot submit proof.
    [Fact]
    public async Task WrongAttempt_ShouldBeRejected()
    {
        var s = await CreateScenarioAsync();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => s.Store.SubmitCurrentStageProofAsync(
                Command(s.Game, Guid.NewGuid()),
                PlayerId,
                s.Now));

        Assert.Equal(0, s.Verifier.CallCount);
    }

    // TEST 7: Expired attempt cannot submit proof.
    [Fact]
    public async Task ExpiredAttempt_ShouldBeRejected()
    {
        var s = await CreateScenarioAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => s.Store.SubmitCurrentStageProofAsync(
                Command(s.Game, s.Attempt.AttemptId),
                PlayerId,
                s.Attempt.DeadlineUtc));

        Assert.Equal(0, s.Verifier.CallCount);
    }

    // TEST 8-9: Moderated games cannot accept proof.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ModeratedGame_ShouldRejectProof(
        bool flagged)
    {
        var s = await CreateScenarioAsync();

        if (flagged)
        {
            s.Game.FlagForReview(
                "Suspicious Game",
                s.Now);
        }
        else
        {
            s.Game.DisableGame(
                "Safety restriction",
                s.Now);
        }

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => s.Store.SubmitCurrentStageProofAsync(
                Command(s.Game, s.Attempt.AttemptId),
                PlayerId,
                s.Now));

        Assert.Equal(0, s.Verifier.CallCount);
    }

    // TEST 10: UTC time is mandatory.
    [Fact]
    public async Task NonUtcTime_ShouldBeRejected()
    {
        var s = await CreateScenarioAsync();

        var nonUtc = s.Now.ToOffset(
            TimeSpan.FromHours(5.5));

        await Assert.ThrowsAsync<ArgumentException>(
            () => s.Store.SubmitCurrentStageProofAsync(
                Command(s.Game, s.Attempt.AttemptId),
                PlayerId,
                nonUtc));

        Assert.Equal(0, s.Verifier.CallCount);
    }

    // TEST 11-12: Empty media or session ID.
    [Theory]
    [InlineData("", "camera-session-001")]
    [InlineData("camera-photo-001", "")]
    public async Task MissingCaptureFields_ShouldBeRejected(
        string mediaId,
        string sessionId)
    {
        var s = await CreateScenarioAsync();

        await Assert.ThrowsAsync<ArgumentException>(
            () => s.Store.SubmitCurrentStageProofAsync(
                Command(
                    s.Game,
                    s.Attempt.AttemptId,
                    mediaId: mediaId,
                    sessionId: sessionId),
                PlayerId,
                s.Now));

        Assert.Equal(0, s.Verifier.CallCount);
    }

    // TEST 13: Verifier is required.
    [Fact]
    public async Task MissingVerifier_ShouldRejectProof()
    {
        var s = await CreateScenarioAsync(
            attachVerifier: false);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => s.Store.SubmitCurrentStageProofAsync(
                Command(s.Game, s.Attempt.AttemptId),
                PlayerId,
                s.Now));
    }

    // TEST 14: Cancellation is respected.
    [Fact]
    public async Task CancelledRequest_ShouldRejectProof()
    {
        var s = await CreateScenarioAsync();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => s.Store.SubmitCurrentStageProofAsync(
                Command(s.Game, s.Attempt.AttemptId),
                PlayerId,
                s.Now,
                cts.Token));

        Assert.Equal(0, s.Verifier.CallCount);
    }

    // TEST 15: Camera verifier receives
    // the authenticated attempt context.
    [Fact]
    public async Task Verifier_ShouldReceiveAttemptContext()
    {
        var s = await CreateScenarioAsync();

        await s.Store.SubmitCurrentStageProofAsync(
            Command(
                s.Game,
                s.Attempt.AttemptId,
                mediaId: "  camera-photo-001  ",
                sessionId: "  camera-session-001  "),
            "  player-001  ",
            s.Now);

        Assert.Equal("player-001",
            s.Verifier.LastPlayerId);
        Assert.Equal("camera-photo-001",
            s.Verifier.LastMediaId);
        Assert.Equal("camera-session-001",
            s.Verifier.LastSessionId);
        Assert.Equal(s.Game.Id, s.Verifier.LastGameId);
        Assert.Equal(s.Attempt.AttemptId,
            s.Verifier.LastAttemptId);
        Assert.Equal(1, s.Verifier.LastStepNumber);
    }

    // TEST 16: Concurrent requests must not
    // submit the same stage twice.
    [Fact]
    public async Task ConcurrentProofs_ShouldAcceptOnlyOne()
    {
        var s = await CreateScenarioAsync();
        var command = Command(
            s.Game, s.Attempt.AttemptId);

        async Task<bool> TrySubmitAsync()
        {
            try
            {
                await s.Store.SubmitCurrentStageProofAsync(
                    command, PlayerId, s.Now);

                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        var results = await Task.WhenAll(
            Enumerable.Range(0, 10)
                .Select(_ => Task.Run(TrySubmitAsync)));

        Assert.Equal(1, results.Count(x => x));
        Assert.Equal(9, results.Count(x => !x));
        Assert.Equal(1, s.Verifier.CallCount);
    }

    // M5-MS06-T04
    // A consumed camera session or media must not
    // be reused by a different player.
    [Theory]
    [InlineData("camera-session-001", "camera-photo-002")]
    [InlineData("camera-session-002", "camera-photo-001")]
    public async Task ReplayedCameraIdentifiers_AcrossPlayers_ShouldBeRejected(
        string replaySessionId,
        string replayMediaId)
    {
        var s = await CreateScenarioAsync();

        // Player 1 submits valid evidence.
        await s.Store.SubmitCurrentStageProofAsync(
            Command(s.Game, s.Attempt.AttemptId),
            PlayerId,
            s.Now);

        // Player 2 starts a separate valid attempt.
        const string otherPlayerId = "player-002";
        var later = s.Now.AddMinutes(1);

        var otherAttempt = await s.Store.StartOrResumeAsync(
            s.Game.Id,
            otherPlayerId,
            later);

        // Attempt to replay an already used session
        // or already used media with another identifier.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => s.Store.SubmitCurrentStageProofAsync(
                Command(
                    s.Game,
                    otherAttempt.AttemptId,
                    mediaId: replayMediaId,
                    sessionId: replaySessionId),
                otherPlayerId,
                later));

        // Replay must be rejected before verifier call.
        Assert.Equal(1, s.Verifier.CallCount);

        // Player 2's progress must not change.
        var stage = await s.Store.GetCurrentStageAsync(
            s.Game.Id,
            otherAttempt.AttemptId,
            otherPlayerId,
            later);

        Assert.Equal(
            GameProgressStatus.InProgress,
            stage.ProgressStatus);

        Assert.Equal(1, stage.CurrentStepNumber);
    }

    // =====================================
    // TEST SCENARIO
    // =====================================
    private static async Task<(
        InMemoryGameSubmissionStore Store,
        Game Game,
        StartGameAttemptResultDto Attempt,
        DateTimeOffset Now,
        FakeCaptureVerifier Verifier)>
        CreateScenarioAsync(bool attachVerifier = true)
    {
        var verifier = new FakeCaptureVerifier();

        var store = attachVerifier
            ? new InMemoryGameSubmissionStore(verifier)
            : new InMemoryGameSubmissionStore();

        var submittedAt = new DateTimeOffset(
            2026, 11, 15, 8, 0, 0,
            TimeSpan.Zero);

        var approvedAt = submittedAt.AddMinutes(30);
        var now = approvedAt.AddMinutes(5);

        var game = Game.CreateDraft(
            Guid.NewGuid(),
            CreatorId,
            "Jaffna Treasure Game",
            "Two stage treasure hunt",
            60);

        var stage1 = new GameStep(
            Guid.NewGuid(), game.Id, 1);

        var stage2 = new GameStep(
            Guid.NewGuid(), game.Id, 2);

        var hint1 = new GameHint(
            Guid.NewGuid(),
            stage1.Id,
            "Find bridge",
            "hint-photo-001");

        var hint2 = new GameHint(
            Guid.NewGuid(),
            stage2.Id,
            "Find lighthouse",
            "hint-photo-002");

        game.ReplaceDraftStages(
            CreatorId,
            new[] { stage1, stage2 },
            new[] { hint1, hint2 });

        store.AddDraft(game);

        await store.SubmitDraftAndEnqueueAsync(
            new ApprovalIdentitySnapshotDto(
                CreatorId,
                "Game Creator",
                game.Id,
                submittedAt));

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

        var attempt = await store.StartOrResumeAsync(
            game.Id,
            PlayerId,
            now);

        return (store, game, attempt, now, verifier);
    }

    // Test-only verifier.
    // This is NOT a production camera verifier.
    private sealed class FakeCaptureVerifier
        : IGameProofCaptureVerifier
    {
        public bool Allow { get; set; } = true;
        public int CallCount { get; private set; }
        public string? LastPlayerId { get; private set; }
        public string? LastMediaId { get; private set; }
        public string? LastSessionId { get; private set; }
        public Guid LastGameId { get; private set; }
        public Guid LastAttemptId { get; private set; }
        public int LastStepNumber { get; private set; }

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

            CallCount++;
            LastSessionId = captureSessionId;
            LastMediaId = mediaId;
            LastGameId = gameId;
            LastAttemptId = attemptId;
            LastStepNumber = stepNumber;
            LastPlayerId = authenticatedPlayerUserId;

            return Task.FromResult(Allow);
        }
    }

    // =====================================
    // M5-MS05-T07
    // RECONNECT MUST NOT UNLOCK STAGE 2
    // =====================================

    [Fact]
    public async Task Reconnect_ShouldNotAllowStageSkip()
    {
        var s = await CreateScenarioAsync();

        // Player reconnects ten minutes later.
        var reconnectAt = s.Now.AddMinutes(10);

        var resumed = await s.Store.StartOrResumeAsync(
            s.Game.Id,
            PlayerId,
            reconnectAt);

        Assert.True(resumed.ResumedExistingAttempt);
        Assert.Equal(
            s.Attempt.AttemptId,
            resumed.AttemptId);

        Assert.Equal(
            s.Attempt.DeadlineUtc,
            resumed.DeadlineUtc);

        // Try to skip Stage 1 by submitting
        // proof directly for Stage 2.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => s.Store.SubmitCurrentStageProofAsync(
                Command(
                    s.Game,
                    resumed.AttemptId,
                    stepNumber: 2),
                PlayerId,
                reconnectAt));

        // Invalid submission must not
        // consume camera capture session.
        Assert.Equal(0, s.Verifier.CallCount);

        var stage = await s.Store.GetCurrentStageAsync(
            s.Game.Id,
            resumed.AttemptId,
            PlayerId,
            reconnectAt);

        Assert.Equal(1, stage.CurrentStepNumber);

        Assert.Equal(
            GameProgressStatus.InProgress,
            stage.ProgressStatus);

        Assert.DoesNotContain(
            stage.Hints,
            hint => hint.TextHint == "Find lighthouse");
    }

    // =====================================
    // M5-MS05-T07
    // PENDING PROOF MUST NOT UNLOCK STAGE 2
    // =====================================

    [Fact]
    public async Task PendingStageOneProof_ShouldNotUnlockStageTwo()
    {
        var s = await CreateScenarioAsync();

        // Submit valid Stage 1 proof.
        await s.Store.SubmitCurrentStageProofAsync(
            Command(
                s.Game,
                s.Attempt.AttemptId,
                stepNumber: 1),
            PlayerId,
            s.Now);

        Assert.Equal(1, s.Verifier.CallCount);

        // Player tries Stage 2 while
        // Stage 1 awaits verification.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => s.Store.SubmitCurrentStageProofAsync(
                Command(
                    s.Game,
                    s.Attempt.AttemptId,
                    stepNumber: 2,
                    mediaId: "camera-photo-002",
                    sessionId: "camera-session-002"),
                PlayerId,
                s.Now.AddMinutes(1)));

        // No second capture verification allowed.
        Assert.Equal(1, s.Verifier.CallCount);

        var stage = await s.Store.GetCurrentStageAsync(
            s.Game.Id,
            s.Attempt.AttemptId,
            PlayerId,
            s.Now.AddMinutes(1));

        // Still on Stage 1.
        Assert.Equal(1, stage.CurrentStepNumber);

        Assert.Equal(
            GameProgressStatus.WaitingForVerification,
            stage.ProgressStatus);

        Assert.DoesNotContain(
            stage.Hints,
            hint => hint.TextHint == "Find lighthouse");
    }

}
