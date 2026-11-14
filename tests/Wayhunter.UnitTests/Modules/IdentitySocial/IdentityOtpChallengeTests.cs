
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Xunit;

namespace Wayhunter.UnitTests.Modules.IdentitySocial;

public sealed class IdentityOtpChallengeTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 11, 14, 12, 0, 0, TimeSpan.Zero);

    private static IdentityOtpChallenge CreateChallenge(
        int maxAttempts = 5)
    {
        return IdentityOtpChallenge.Create(
            "user-001",
            "test-only-otp-verifier",
            Now,
            Now.AddMinutes(10),
            maxAttempts);
    }

    [Fact]
    public void Create_ShouldInitializeOtpChallenge()
    {
        var challenge = CreateChallenge();

        Assert.False(
            string.IsNullOrWhiteSpace(challenge.ChallengeId));

        Assert.Equal("user-001", challenge.UserId);
        Assert.Equal(0, challenge.FailedAttemptCount);
        Assert.Equal(5, challenge.MaxFailedAttempts);
        Assert.Null(challenge.ConsumedAtUtc);
        Assert.Null(challenge.RevokedAtUtc);
    }

    [Fact]
    public void ExpiredOtp_ShouldNotAllowAttempts()
    {
        var challenge = CreateChallenge();

        Assert.True(challenge.CanAttemptAt(
            Now.AddMinutes(9)));

        Assert.False(challenge.CanAttemptAt(
            Now.AddMinutes(10)));

        Assert.Throws<InvalidOperationException>(() =>
            challenge.RecordFailedAttempt(
                Now.AddMinutes(11)));
    }

    [Fact]
    public void FailedAttempts_ShouldLockChallengeAtLimit()
    {
        var challenge = CreateChallenge(maxAttempts: 3);

        challenge.RecordFailedAttempt(Now.AddMinutes(1));
        challenge.RecordFailedAttempt(Now.AddMinutes(2));
        challenge.RecordFailedAttempt(Now.AddMinutes(3));

        Assert.Equal(3, challenge.FailedAttemptCount);

        Assert.False(challenge.CanAttemptAt(
            Now.AddMinutes(4)));

        Assert.Throws<InvalidOperationException>(() =>
            challenge.RecordFailedAttempt(
                Now.AddMinutes(4)));
    }

    [Fact]
    public void ConsumedOtp_ShouldNotBeReusable()
    {
        var challenge = CreateChallenge();

        challenge.ConsumeAfterExternalVerification(
            Now.AddMinutes(2));

        Assert.Equal(
            Now.AddMinutes(2),
            challenge.ConsumedAtUtc);

        Assert.False(challenge.CanAttemptAt(
            Now.AddMinutes(3)));

        Assert.Throws<InvalidOperationException>(() =>
            challenge.ConsumeAfterExternalVerification(
                Now.AddMinutes(3)));
    }

    [Fact]
    public void RevokedOtp_ShouldNotAllowAttempts()
    {
        var challenge = CreateChallenge();

        challenge.Revoke(Now.AddMinutes(1));

        Assert.Equal(
            Now.AddMinutes(1),
            challenge.RevokedAtUtc);

        Assert.False(challenge.CanAttemptAt(
            Now.AddMinutes(2)));
    }

    [Fact]
    public void InvalidExpiry_ShouldBeRejected()
    {
        Assert.Throws<ArgumentException>(() =>
            IdentityOtpChallenge.Create(
                "user-001",
                "test-only-otp-verifier",
                Now,
                Now.AddMinutes(-1)));
    }
}
