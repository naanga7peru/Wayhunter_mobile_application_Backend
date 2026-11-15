
using System;
using Wayhunter.Application.Modules.IdentitySocial.Services;
using Xunit;

namespace Wayhunter.UnitTests.Modules.IdentitySocial;

public sealed class OtpResendCooldownPolicyTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 11, 15, 12, 0, 0, TimeSpan.Zero);

    private static readonly TimeSpan Cooldown =
        TimeSpan.FromSeconds(60);

    [Fact]
    public void FirstOtpRequest_ShouldBeAllowed()
    {
        var result = OtpResendCooldownPolicy.Check(
            Now,
            null,
            Cooldown);

        Assert.True(result.Allowed);
        Assert.Equal(TimeSpan.Zero, result.RetryAfter);
    }

    [Fact]
    public void ResendDuringCooldown_ShouldBeBlocked()
    {
        var lastSent = Now.AddSeconds(-30);

        var result = OtpResendCooldownPolicy.Check(
            Now,
            lastSent,
            Cooldown);

        Assert.False(result.Allowed);

        Assert.Equal(
            TimeSpan.FromSeconds(30),
            result.RetryAfter);
    }

    [Fact]
    public void ResendAtCooldownBoundary_ShouldBeAllowed()
    {
        var lastSent = Now.AddSeconds(-60);

        var result = OtpResendCooldownPolicy.Check(
            Now,
            lastSent,
            Cooldown);

        Assert.True(result.Allowed);
        Assert.Equal(TimeSpan.Zero, result.RetryAfter);
    }

    [Fact]
    public void ResendAfterCooldown_ShouldBeAllowed()
    {
        var lastSent = Now.AddMinutes(-2);

        var result = OtpResendCooldownPolicy.Check(
            Now,
            lastSent,
            Cooldown);

        Assert.True(result.Allowed);
        Assert.Equal(TimeSpan.Zero, result.RetryAfter);
    }

    [Fact]
    public void InvalidCurrentTime_ShouldBeRejected()
    {
        Assert.Throws<ArgumentException>(() =>
            OtpResendCooldownPolicy.Check(
                default,
                null,
                Cooldown));

        var nonUtc = new DateTimeOffset(
            2026, 11, 15, 12, 0, 0,
            TimeSpan.FromHours(5));

        Assert.Throws<ArgumentException>(() =>
            OtpResendCooldownPolicy.Check(
                nonUtc,
                null,
                Cooldown));
    }

    [Fact]
    public void InvalidLastSentTime_ShouldBeRejected()
    {
        var future = Now.AddSeconds(10);

        Assert.Throws<ArgumentException>(() =>
            OtpResendCooldownPolicy.Check(
                Now,
                future,
                Cooldown));

        var nonUtc = new DateTimeOffset(
            2026, 11, 15, 11, 0, 0,
            TimeSpan.FromHours(1));

        Assert.Throws<ArgumentException>(() =>
            OtpResendCooldownPolicy.Check(
                Now,
                nonUtc,
                Cooldown));
    }

    [Fact]
    public void InvalidCooldown_ShouldBeRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            OtpResendCooldownPolicy.Check(
                Now,
                null,
                TimeSpan.Zero));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            OtpResendCooldownPolicy.Check(
                Now,
                null,
                TimeSpan.FromSeconds(-1)));
    }
}
