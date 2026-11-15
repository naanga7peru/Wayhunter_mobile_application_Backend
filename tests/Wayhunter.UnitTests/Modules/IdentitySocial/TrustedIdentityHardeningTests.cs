
using System;
using System.Security.Claims;
using Wayhunter.Api.Contracts;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Xunit;

namespace Wayhunter.UnitTests.Modules.IdentitySocial;

public sealed class TrustedIdentityHardeningTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 11, 15, 10, 0, 0, TimeSpan.Zero);

    private static ClaimsPrincipal Principal(
        params Claim[] claims)
    {
        return new ClaimsPrincipal(
            new ClaimsIdentity(claims, "TestAuth"));
    }

    private static UserAccount ActiveAccount()
    {
        var account = UserAccount.RegisterPending(
            "Test Traveler",
            $"test-{Guid.NewGuid():N}@example.com",
            "test-only-password-hash",
            Now);

        account.ActivateAfterVerifiedOtp(
            Now.AddMinutes(5));

        return account;
    }

    [Fact]
    public void DuplicateNameIdentifierClaims_AreRejected()
    {
        var id = Guid.NewGuid().ToString("D");

        var principal = Principal(
            new Claim(ClaimTypes.NameIdentifier, id),
            new Claim(ClaimTypes.NameIdentifier, id));

        Assert.False(
            TrustedIdentityAccessContract.TryResolveUserId(
                principal, out _));
    }

    [Fact]
    public void DuplicateSubjectClaims_AreRejected()
    {
        var id = Guid.NewGuid().ToString("D");

        var principal = Principal(
            new Claim("sub", id),
            new Claim("sub", id));

        Assert.False(
            TrustedIdentityAccessContract.TryResolveUserId(
                principal, out _));
    }

    [Fact]
    public void MatchingIdentityClaims_AreAccepted()
    {
        var id = Guid.NewGuid().ToString("D");

        var principal = Principal(
            new Claim(ClaimTypes.NameIdentifier, id),
            new Claim("sub", id.ToUpperInvariant()));

        Assert.True(
            TrustedIdentityAccessContract.TryResolveUserId(
                principal, out var result));

        Assert.Equal(id, result);
    }

    [Fact]
    public void ConflictingIdentityClaims_AreRejected()
    {
        var principal = Principal(
            new Claim(
                ClaimTypes.NameIdentifier,
                Guid.NewGuid().ToString("D")),
            new Claim(
                "sub",
                Guid.NewGuid().ToString("D")));

        Assert.False(
            TrustedIdentityAccessContract.TryResolveUserId(
                principal, out _));
    }

    [Fact]
    public void EmptyGuid_IsRejected()
    {
        var principal = Principal(
            new Claim(
                ClaimTypes.NameIdentifier,
                Guid.Empty.ToString("D")));

        Assert.False(
            TrustedIdentityAccessContract.TryResolveUserId(
                principal, out _));
    }

    [Fact]
    public void InvalidIdentityClaim_IsRejected()
    {
        var principal = Principal(
            new Claim(
                ClaimTypes.NameIdentifier,
                "invalid-guid"));

        Assert.False(
            TrustedIdentityAccessContract.TryResolveUserId(
                principal, out _));
    }

    [Fact]
    public void InvalidNameIdCannotFallbackToValidSub()
    {
        var principal = Principal(
            new Claim(
                ClaimTypes.NameIdentifier,
                "invalid-guid"),
            new Claim(
                "sub",
                Guid.NewGuid().ToString("D")));

        Assert.False(
            TrustedIdentityAccessContract.TryResolveUserId(
                principal, out _));
    }

    [Fact]
    public void ActiveVerifiedAccount_IsAllowed()
    {
        var account = ActiveAccount();

        Assert.True(
            TrustedIdentityAccessContract.IsActiveAccountOwner(
                account.UserId, account));
    }

    [Fact]
    public void SuspendedAccount_IsRejected()
    {
        var account = ActiveAccount();
        account.Suspend();

        Assert.False(
            TrustedIdentityAccessContract.IsActiveAccountOwner(
                account.UserId, account));
    }

    [Fact]
    public void DeletionRequestedAccount_IsRejected()
    {
        var account = ActiveAccount();

        account.RequestDeletion(
            Now.AddMinutes(10));

        Assert.False(
            TrustedIdentityAccessContract.IsActiveAccountOwner(
                account.UserId, account));
    }

    [Fact]
    public void WrongAccountOwner_IsRejected()
    {
        var account = ActiveAccount();

        Assert.False(
            TrustedIdentityAccessContract.IsActiveAccountOwner(
                Guid.NewGuid().ToString("D"),
                account));
    }

    [Fact]
    public void MissingAccount_IsRejected()
    {
        Assert.False(
            TrustedIdentityAccessContract.IsActiveAccountOwner(
                Guid.NewGuid().ToString("D"),
                null));
    }
}
