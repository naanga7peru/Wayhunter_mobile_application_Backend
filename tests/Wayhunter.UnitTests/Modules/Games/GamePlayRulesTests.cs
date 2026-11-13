
using System;
using Wayhunter.Domain.Modules.Games.Entities;
using Wayhunter.Domain.Modules.Games.Services;
using Xunit;

namespace Wayhunter.UnitTests.Modules.Games;

public sealed class GamePlayRulesTests
{
    private static readonly DateTimeOffset ApprovedAt =
        new(2026, 11, 13, 10, 0, 0, TimeSpan.Zero);

    private static (
        Game Game,
        GameAttempt Attempt,
        GameProgress Progress,
        GameEvidence Evidence,
        DateTimeOffset Now) ValidScenario()
    {
        var game = new Game(
            Guid.NewGuid(), "creator-1");

        game.ApproveCreation(ApprovedAt);

        var attempt = new GameAttempt(
            Guid.NewGuid(),
            game.Id,
            "player-1",
            ApprovedAt.AddMinutes(1));

        var progress = new GameProgress(
            attempt.Id, 3);

        progress.Start();

        var evidence = new GameEvidence(
            Guid.NewGuid(),
            attempt.Id,
            1,
            "camera-photo-001",
            ApprovedAt.AddMinutes(2));

        return (
            game,
            attempt,
            progress,
            evidence,
            ApprovedAt.AddMinutes(3));
    }

    [Fact]
    public void ValidEvidence_ShouldBeAllowed()
    {
        var s = ValidScenario();

        GamePlayRules.EnsureCanSubmitEvidence(
            s.Game, s.Attempt, s.Progress,
            s.Evidence, "player-1", s.Now);
    }

    [Fact]
    public void Creator_ShouldNotStartOwnGame()
    {
        var s = ValidScenario();

        Assert.Throws<InvalidOperationException>(() =>
            GamePlayRules.EnsureCanStart(
                s.Game, "creator-1", s.Now));
    }

    [Fact]
    public void PendingGame_ShouldNotBePlayable()
    {
        var game = new Game(
            Guid.NewGuid(), "creator-1");

        Assert.Throws<InvalidOperationException>(() =>
            GamePlayRules.EnsureCanStart(
                game, "player-1", ApprovedAt));
    }

    [Fact]
    public void ExpiredGame_ShouldNotBePlayable()
    {
        var s = ValidScenario();

        Assert.Throws<InvalidOperationException>(() =>
            GamePlayRules.EnsureCanStart(
                s.Game,
                "player-1",
                ApprovedAt.AddDays(30)));
    }

    [Fact]
    public void OtherPlayer_ShouldNotSubmitEvidence()
    {
        var s = ValidScenario();

        Assert.Throws<InvalidOperationException>(() =>
            GamePlayRules.EnsureCanSubmitEvidence(
                s.Game, s.Attempt, s.Progress,
                s.Evidence, "player-2", s.Now));
    }

    [Fact]
    public void AttemptFromAnotherGame_ShouldBeRejected()
    {
        var s = ValidScenario();

        var wrongAttempt = new GameAttempt(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "player-1",
            ApprovedAt.AddMinutes(1));

        Assert.Throws<InvalidOperationException>(() =>
            GamePlayRules.EnsureCanSubmitEvidence(
                s.Game, wrongAttempt, s.Progress,
                s.Evidence, "player-1", s.Now));
    }

    [Fact]
    public void ProgressFromAnotherAttempt_ShouldBeRejected()
    {
        var s = ValidScenario();

        var wrongProgress = new GameProgress(
            Guid.NewGuid(), 3);

        wrongProgress.Start();

        Assert.Throws<InvalidOperationException>(() =>
            GamePlayRules.EnsureCanSubmitEvidence(
                s.Game, s.Attempt, wrongProgress,
                s.Evidence, "player-1", s.Now));
    }

    [Fact]
    public void EvidenceFromAnotherAttempt_ShouldBeRejected()
    {
        var s = ValidScenario();

        var wrongEvidence = new GameEvidence(
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            "camera-photo-002",
            ApprovedAt.AddMinutes(2));

        Assert.Throws<InvalidOperationException>(() =>
            GamePlayRules.EnsureCanSubmitEvidence(
                s.Game, s.Attempt, s.Progress,
                wrongEvidence, "player-1", s.Now));
    }

    [Fact]
    public void WrongStep_ShouldBeRejected()
    {
        var s = ValidScenario();

        var wrongEvidence = new GameEvidence(
            Guid.NewGuid(),
            s.Attempt.Id,
            2,
            "camera-photo-003",
            ApprovedAt.AddMinutes(2));

        Assert.Throws<InvalidOperationException>(() =>
            GamePlayRules.EnsureCanSubmitEvidence(
                s.Game, s.Attempt, s.Progress,
                wrongEvidence, "player-1", s.Now));
    }

    [Fact]
    public void WaitingAttempt_ShouldRejectNewEvidence()
    {
        var s = ValidScenario();

        s.Progress.SubmitEvidence(1);

        Assert.Throws<InvalidOperationException>(() =>
            GamePlayRules.EnsureCanSubmitEvidence(
                s.Game, s.Attempt, s.Progress,
                s.Evidence, "player-1", s.Now));
    }

    [Fact]
    public void EvidenceBeforeAttemptStart_ShouldBeRejected()
    {
        var s = ValidScenario();

        var invalidEvidence = new GameEvidence(
            Guid.NewGuid(),
            s.Attempt.Id,
            1,
            "camera-photo-004",
            s.Attempt.StartedAtUtc.AddSeconds(-1));

        Assert.Throws<InvalidOperationException>(() =>
            GamePlayRules.EnsureCanSubmitEvidence(
                s.Game, s.Attempt, s.Progress,
                invalidEvidence, "player-1", s.Now));
    }

    [Fact]
    public void FutureEvidenceTimestamp_ShouldBeRejected()
    {
        var s = ValidScenario();

        var futureEvidence = new GameEvidence(
            Guid.NewGuid(),
            s.Attempt.Id,
            1,
            "camera-photo-005",
            s.Now.AddMinutes(1));

        Assert.Throws<InvalidOperationException>(() =>
            GamePlayRules.EnsureCanSubmitEvidence(
                s.Game, s.Attempt, s.Progress,
                futureEvidence, "player-1", s.Now));
    }
}
