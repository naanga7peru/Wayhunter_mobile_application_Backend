
using System;
using Wayhunter.Domain.Modules.Games.Services;
using Xunit;

namespace Wayhunter.UnitTests.Modules.Games;

public sealed class GameAttemptDeadlineRulesTests
{
    private static readonly DateTimeOffset StartedAt =
        new(2026, 11, 15, 8, 0, 0, TimeSpan.Zero);

    // TEST 1: Normal duration.
    [Fact]
    public void CalculateDeadline_ShouldUseGameDuration()
    {
        var deadline =
            GameAttemptDeadlineRules.CalculateDeadlineUtc(
                StartedAt,
                60,
                StartedAt.AddDays(30));

        Assert.Equal(
            StartedAt.AddMinutes(60),
            deadline);
    }

    // TEST 2: Attempt cannot exceed Game expiry.
    [Fact]
    public void CalculateDeadline_ShouldCapAtGameExpiry()
    {
        var gameExpiry = StartedAt.AddMinutes(15);

        var deadline =
            GameAttemptDeadlineRules.CalculateDeadlineUtc(
                StartedAt,
                60,
                gameExpiry);

        Assert.Equal(gameExpiry, deadline);
    }

    // TEST 3: Exact expiry is blocked.
    [Fact]
    public void CalculateDeadline_AtExpiry_ShouldReject()
    {
        Assert.Throws<InvalidOperationException>(() =>
            GameAttemptDeadlineRules.CalculateDeadlineUtc(
                StartedAt,
                60,
                StartedAt));
    }

    // TEST 4: Game already expired.
    [Fact]
    public void CalculateDeadline_AfterExpiry_ShouldReject()
    {
        Assert.Throws<InvalidOperationException>(() =>
            GameAttemptDeadlineRules.CalculateDeadlineUtc(
                StartedAt,
                60,
                StartedAt.AddSeconds(-1)));
    }

    // TEST 5-6: Duration must be positive.
    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void CalculateDeadline_InvalidDuration_ShouldReject(
        int durationMinutes)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            GameAttemptDeadlineRules.CalculateDeadlineUtc(
                StartedAt,
                durationMinutes,
                StartedAt.AddDays(30)));
    }

    // TEST 7: Start timestamp must be UTC.
    [Fact]
    public void CalculateDeadline_NonUtcStart_ShouldReject()
    {
        var nonUtc = StartedAt.ToOffset(
            TimeSpan.FromHours(5.5));

        Assert.Throws<ArgumentException>(() =>
            GameAttemptDeadlineRules.CalculateDeadlineUtc(
                nonUtc,
                60,
                StartedAt.AddDays(30)));
    }

    // TEST 8: Game expiry must be UTC.
    [Fact]
    public void CalculateDeadline_NonUtcExpiry_ShouldReject()
    {
        var nonUtcExpiry =
            StartedAt.AddDays(30).ToOffset(
                TimeSpan.FromHours(5.5));

        Assert.Throws<ArgumentException>(() =>
            GameAttemptDeadlineRules.CalculateDeadlineUtc(
                StartedAt,
                60,
                nonUtcExpiry));
    }

    // TEST 9: Default timestamps are invalid.
    [Fact]
    public void CalculateDeadline_DefaultTime_ShouldReject()
    {
        Assert.Throws<ArgumentException>(() =>
            GameAttemptDeadlineRules.CalculateDeadlineUtc(
                default,
                60,
                StartedAt.AddDays(30)));

        Assert.Throws<ArgumentException>(() =>
            GameAttemptDeadlineRules.CalculateDeadlineUtc(
                StartedAt,
                60,
                default));
    }

    // TEST 10: Exact start time is valid.
    [Fact]
    public void EnsureActive_AtStart_ShouldAllow()
    {
        GameAttemptDeadlineRules.EnsureActive(
            StartedAt,
            StartedAt.AddMinutes(60),
            StartedAt);
    }

    // TEST 11: Just before deadline is valid.
    [Fact]
    public void EnsureActive_BeforeDeadline_ShouldAllow()
    {
        var deadline = StartedAt.AddMinutes(60);

        GameAttemptDeadlineRules.EnsureActive(
            StartedAt,
            deadline,
            deadline.AddTicks(-1));
    }

    // TEST 12: Before start must be rejected.
    [Fact]
    public void EnsureActive_BeforeStart_ShouldReject()
    {
        Assert.Throws<InvalidOperationException>(() =>
            GameAttemptDeadlineRules.EnsureActive(
                StartedAt,
                StartedAt.AddMinutes(60),
                StartedAt.AddSeconds(-1)));
    }

    // TEST 13: Exact deadline is expired.
    [Fact]
    public void EnsureActive_AtDeadline_ShouldReject()
    {
        var deadline = StartedAt.AddMinutes(60);

        Assert.Throws<InvalidOperationException>(() =>
            GameAttemptDeadlineRules.EnsureActive(
                StartedAt,
                deadline,
                deadline));
    }

    // TEST 14: After deadline is expired.
    [Fact]
    public void EnsureActive_AfterDeadline_ShouldReject()
    {
        var deadline = StartedAt.AddMinutes(60);

        Assert.Throws<InvalidOperationException>(() =>
            GameAttemptDeadlineRules.EnsureActive(
                StartedAt,
                deadline,
                deadline.AddSeconds(1)));
    }

    // TEST 15: Deadline cannot precede start.
    [Fact]
    public void EnsureActive_InvalidDeadline_ShouldReject()
    {
        Assert.Throws<InvalidOperationException>(() =>
            GameAttemptDeadlineRules.EnsureActive(
                StartedAt,
                StartedAt.AddSeconds(-1),
                StartedAt));

        Assert.Throws<InvalidOperationException>(() =>
            GameAttemptDeadlineRules.EnsureActive(
                StartedAt,
                StartedAt,
                StartedAt));
    }

    // TEST 16: Non-UTC current time.
    [Fact]
    public void EnsureActive_NonUtcNow_ShouldReject()
    {
        var nonUtcNow = StartedAt.ToOffset(
            TimeSpan.FromHours(5.5));

        Assert.Throws<ArgumentException>(() =>
            GameAttemptDeadlineRules.EnsureActive(
                StartedAt,
                StartedAt.AddMinutes(60),
                nonUtcNow));
    }

    // TEST 17: Default current time.
    [Fact]
    public void EnsureActive_DefaultNow_ShouldReject()
    {
        Assert.Throws<ArgumentException>(() =>
            GameAttemptDeadlineRules.EnsureActive(
                StartedAt,
                StartedAt.AddMinutes(60),
                default));
    }
}
