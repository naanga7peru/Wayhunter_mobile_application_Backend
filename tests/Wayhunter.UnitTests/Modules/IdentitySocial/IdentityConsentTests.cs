
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Xunit;

namespace Wayhunter.UnitTests.Modules.IdentitySocial;

public sealed class IdentityConsentTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 11, 14, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Grant_ShouldCreateConsentRecord()
    {
        var consent = IdentityConsent.Grant(
            "user-001",
            "privacy-policy",
            Now);

        Assert.False(
            string.IsNullOrWhiteSpace(consent.ConsentId));

        Assert.Equal("user-001", consent.UserId);
        Assert.Equal("privacy-policy", consent.ConsentKey);
        Assert.Equal(Now, consent.GrantedAtUtc);
        Assert.Null(consent.RevokedAtUtc);
    }

    [Fact]
    public void ConsentKey_ShouldBeNormalized()
    {
        var consent = IdentityConsent.Grant(
            "  user-001  ",
            "  LOCATION-PERMISSION  ",
            Now);

        Assert.Equal("user-001", consent.UserId);
        Assert.Equal(
            "location-permission",
            consent.ConsentKey);
    }

    [Fact]
    public void GrantedConsent_ShouldBeActive()
    {
        var consent = IdentityConsent.Grant(
            "user-001",
            "privacy-policy",
            Now);

        Assert.False(
            consent.IsActiveAt(Now.AddMinutes(-1)));

        Assert.True(
            consent.IsActiveAt(Now.AddMinutes(1)));
    }

    [Fact]
    public void RevokedConsent_ShouldNotBeActive()
    {
        var consent = IdentityConsent.Grant(
            "user-001",
            "location-permission",
            Now);

        consent.Revoke(Now.AddMinutes(5));

        Assert.True(
            consent.IsActiveAt(Now.AddMinutes(4)));

        Assert.False(
            consent.IsActiveAt(Now.AddMinutes(5)));

        Assert.Equal(
            Now.AddMinutes(5),
            consent.RevokedAtUtc);
    }

    [Fact]
    public void RepeatedRevocation_ShouldBeRejected()
    {
        var consent = IdentityConsent.Grant(
            "user-001",
            "notifications",
            Now);

        consent.Revoke(Now.AddMinutes(2));

        Assert.Throws<InvalidOperationException>(() =>
            consent.Revoke(Now.AddMinutes(3)));
    }

    [Fact]
    public void InvalidConsentTimestamp_ShouldBeRejected()
    {
        Assert.Throws<ArgumentException>(() =>
            IdentityConsent.Grant(
                "user-001",
                "privacy-policy",
                default));

        var consent = IdentityConsent.Grant(
            "user-001",
            "privacy-policy",
            Now);

        Assert.Throws<ArgumentException>(() =>
            consent.Revoke(Now.AddMinutes(-1)));
    }
}
