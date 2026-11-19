using System;
using Wayhunter.Application.Modules.Games.Validators;
using Xunit;

namespace Wayhunter.UnitTests.Modules.Games;

public sealed class GameProofSubmissionLeaseRulesTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 19, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ValidClaim_ShouldPass()
    {
        GameProofSubmissionLeaseRules.ValidateClaim("claim-1", Now, Now.AddMinutes(5));
    }

    [Fact]
    public void MaximumLengthToken_ShouldPass()
    {
        GameProofSubmissionLeaseRules.ValidateClaim(new string('x', 128), Now, Now.AddMinutes(5));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BlankClaimToken_ShouldReject(string? token)
    {
        Assert.Throws<ArgumentException>(() => GameProofSubmissionLeaseRules.ValidateClaim(token!, Now, Now.AddMinutes(5)));
    }

    [Fact]
    public void OversizedToken_ShouldReject()
    {
        Assert.Throws<ArgumentException>(() => GameProofSubmissionLeaseRules.ValidateClaim(new string('x', 129), Now, Now.AddMinutes(5)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NonFutureLease_ShouldReject(int minutes)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => GameProofSubmissionLeaseRules.ValidateClaim("claim-1", Now, Now.AddMinutes(minutes)));
    }

    [Theory]
    [InlineData(-1, true)]
    [InlineData(0, true)]
    [InlineData(1, false)]
    public void LeaseExpiration_ShouldMatchTime(int minutes, bool expired)
    {
        Assert.Equal(expired, GameProofSubmissionLeaseRules.IsExpired(Now.AddMinutes(minutes), Now));
    }

    [Fact]
    public void MissingLeaseExpiry_ShouldReturnFalse()
    {
        Assert.False(GameProofSubmissionLeaseRules.IsExpired(null, Now));
    }
}
