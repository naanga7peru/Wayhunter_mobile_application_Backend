
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Xunit;

namespace Wayhunter.UnitTests.Modules.IdentitySocial;

public sealed class IdentitySessionTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 11, 14, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_ShouldGenerateSessionId()
    {
        var session = IdentitySession.Create(
            "user-001",
            Now,
            Now.AddHours(1));

        Assert.False(
            string.IsNullOrWhiteSpace(session.SessionId));

        Assert.Equal("user-001", session.UserId);
        Assert.Null(session.RevokedAtUtc);
    }

    [Fact]
    public void ExpiredSession_ShouldNotBeActive()
    {
        var session = IdentitySession.Create(
            "user-001",
            Now,
            Now.AddMinutes(30));

        Assert.True(session.IsActiveAt(Now.AddMinutes(10)));
        Assert.False(session.IsActiveAt(Now.AddMinutes(30)));
    }

    [Fact]
    public void RevokedSession_ShouldNotBeActive()
    {
        var session = IdentitySession.Create(
            "user-001",
            Now,
            Now.AddHours(1));

        session.Revoke(Now.AddMinutes(5));

        Assert.False(session.IsActiveAt(Now.AddMinutes(10)));
        Assert.Equal(Now.AddMinutes(5), session.RevokedAtUtc);
    }

    [Fact]
    public void ExpiryBeforeCreation_ShouldBeRejected()
    {
        Assert.Throws<ArgumentException>(() =>
            IdentitySession.Create(
                "user-001",
                Now,
                Now.AddMinutes(-1)));
    }
}
