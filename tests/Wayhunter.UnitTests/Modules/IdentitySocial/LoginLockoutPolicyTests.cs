
using Wayhunter.Application.Modules.IdentitySocial.Services;
using Xunit;

namespace Wayhunter.UnitTests.Modules.IdentitySocial;

public sealed class LoginLockoutPolicyTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 11, 15, 10, 0, 0, TimeSpan.Zero);

    private static readonly LoginLockoutOptions Options =
        new(5, TimeSpan.FromMinutes(15));

    private static LoginLockoutState EmptyState()
        => new(0, null);

    [Fact]
    public void NewAccount_AllowsLogin()
    {
        var decision = LoginLockoutPolicy.Check(
            EmptyState(), Now);

        Assert.True(decision.IsAllowed);
        Assert.Equal(TimeSpan.Zero, decision.RetryAfter);
    }

    [Fact]
    public void FirstFailure_IncrementsCounter()
    {
        var state = LoginLockoutPolicy.RecordFailure(
            EmptyState(), Options, Now);

        Assert.Equal(1, state.FailedAttempts);
        Assert.Null(state.LockedUntilUtc);
    }

    [Fact]
    public void BelowThreshold_DoesNotLock()
    {
        var state = EmptyState();

        for (var i = 0; i < 4; i++)
        {
            state = LoginLockoutPolicy.RecordFailure(
                state, Options, Now);
        }

        Assert.Equal(4, state.FailedAttempts);
        Assert.Null(state.LockedUntilUtc);
        Assert.True(
            LoginLockoutPolicy.Check(state, Now).IsAllowed);
    }

    [Fact]
    public void FifthFailure_TriggersLockout()
    {
        var state = EmptyState();

        for (var i = 0; i < 5; i++)
        {
            state = LoginLockoutPolicy.RecordFailure(
                state, Options, Now);
        }

        Assert.Equal(5, state.FailedAttempts);
        Assert.Equal(
            Now.AddMinutes(15),
            state.LockedUntilUtc);

        var decision = LoginLockoutPolicy.Check(
            state, Now);

        Assert.False(decision.IsAllowed);
        Assert.Equal(
            TimeSpan.FromMinutes(15),
            decision.RetryAfter);
    }

    [Fact]
    public void FailedAttemptDuringLockout_DoesNotExtendLock()
    {
        var state = new LoginLockoutState(
            5,
            Now.AddMinutes(15));

        var updated = LoginLockoutPolicy.RecordFailure(
            state,
            Options,
            Now.AddMinutes(5));

        Assert.Equal(state, updated);
    }

    [Fact]
    public void ExpiredLockout_AllowsNewAttempt()
    {
        var state = new LoginLockoutState(
            5,
            Now.AddMinutes(15));

        var updated = LoginLockoutPolicy.RecordFailure(
            state,
            Options,
            Now.AddMinutes(15));

        Assert.Equal(1, updated.FailedAttempts);
        Assert.Null(updated.LockedUntilUtc);
    }

    [Fact]
    public void SuccessfulLogin_ResetsFailureCounter()
    {
        var state = new LoginLockoutState(3, null);

        var updated = LoginLockoutPolicy.RecordSuccess(
            state, Now);

        Assert.Equal(0, updated.FailedAttempts);
        Assert.Null(updated.LockedUntilUtc);
    }

    [Fact]
    public void ActiveLockout_CannotBeClearedBySuccess()
    {
        var state = new LoginLockoutState(
            5,
            Now.AddMinutes(15));

        Assert.Throws<InvalidOperationException>(() =>
            LoginLockoutPolicy.RecordSuccess(
                state, Now.AddMinutes(5)));
    }

    [Fact]
    public void InvalidThreshold_IsRejected()
    {
        var options = new LoginLockoutOptions(
            0,
            TimeSpan.FromMinutes(15));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            LoginLockoutPolicy.RecordFailure(
                EmptyState(), options, Now));
    }

    [Fact]
    public void InvalidDuration_IsRejected()
    {
        var options = new LoginLockoutOptions(
            5,
            TimeSpan.Zero);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            LoginLockoutPolicy.RecordFailure(
                EmptyState(), options, Now));
    }

    [Fact]
    public void NegativeFailureCount_IsRejected()
    {
        var state = new LoginLockoutState(-1, null);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            LoginLockoutPolicy.Check(state, Now));
    }

    [Fact]
    public void NonUtcTimestamp_IsRejected()
    {
        var localTime = Now.ToOffset(
            TimeSpan.FromHours(5.5));

        Assert.Throws<ArgumentException>(() =>
            LoginLockoutPolicy.Check(
                EmptyState(), localTime));
    }
}
