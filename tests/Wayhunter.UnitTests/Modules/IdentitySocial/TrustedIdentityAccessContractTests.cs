
using System;
using System.Security.Claims;
using Wayhunter.Api.Contracts;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Xunit;

namespace Wayhunter.UnitTests.Modules.IdentitySocial;

public sealed class TrustedIdentityAccessContractTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 11, 15, 8, 0, 0, TimeSpan.Zero);

    private static ClaimsPrincipal CreatePrincipal(
        string? nameId,
        string? subject = null,
        bool authenticated = true)
    {
        var claims = new System.Collections.Generic.List<Claim>();

        if (nameId is not null)
            claims.Add(new Claim(ClaimTypes.NameIdentifier, nameId));

        if (subject is not null)
            claims.Add(new Claim("sub", subject));

        return new ClaimsPrincipal(new ClaimsIdentity(
            claims,
            authenticated ? "TestAuth" : null));
    }

    private static UserAccount CreateActiveAccount()
    {
        var account = UserAccount.RegisterPending(
            "Test Hunter",
            "hunter@example.com",
            "test-password-hash",
            Now);

        account.ActivateAfterVerifiedOtp(Now.AddMinutes(5));
        return account;
    }

    [Fact]
    public void ValidGuidClaim_ResolvesUserId()
    {
        var id = Guid.NewGuid().ToString("D");
        var principal = CreatePrincipal(id);

        Assert.True(
            TrustedIdentityAccessContract.TryResolveUserId(
                principal, out var result));

        Assert.Equal(id, result);
    }

    [Fact]
    public void ValidSubClaim_ResolvesUserId()
    {
        var id = Guid.NewGuid().ToString("D");

        Assert.True(
            TrustedIdentityAccessContract.TryResolveUserId(
                CreatePrincipal(null, id), out var result));

        Assert.Equal(id, result);
    }

    [Fact]
    public void InvalidGuid_IsRejected()
    {
        Assert.False(
            TrustedIdentityAccessContract.TryResolveUserId(
                CreatePrincipal("invalid-user-id"), out _));
    }

    [Fact]
    public void AnonymousPrincipal_IsRejected()
    {
        Assert.False(
            TrustedIdentityAccessContract.TryResolveUserId(
                CreatePrincipal(
                    Guid.NewGuid().ToString("D"),
                    authenticated: false), out _));
    }

    [Fact]
    public void ConflictingClaims_AreRejected()
    {
        Assert.False(
            TrustedIdentityAccessContract.TryResolveUserId(
                CreatePrincipal(
                    Guid.NewGuid().ToString("D"),
                    Guid.NewGuid().ToString("D")), out _));
    }

    [Fact]
    public void ActiveAccountWithMatchingId_IsAllowed()
    {
        var account = CreateActiveAccount();

        Assert.True(
            TrustedIdentityAccessContract.IsActiveAccountOwner(
                account.UserId, account));
    }

    [Fact]
    public void PendingAccount_IsRejected()
    {
        var account = UserAccount.RegisterPending(
            "Pending Hunter",
            "pending@example.com",
            "test-password-hash",
            Now);

        Assert.False(
            TrustedIdentityAccessContract.IsActiveAccountOwner(
                account.UserId, account));
    }

    [Fact]
    public void SuspendedAccount_IsRejected()
    {
        var account = CreateActiveAccount();
        account.Suspend();

        Assert.False(
            TrustedIdentityAccessContract.IsActiveAccountOwner(
                account.UserId, account));
    }

    [Fact]
    public void WrongOwner_IsRejected()
    {
        var account = CreateActiveAccount();

        Assert.False(
            TrustedIdentityAccessContract.IsActiveAccountOwner(
                Guid.NewGuid().ToString("D"), account));
    }

    [Fact]
    public void MissingAccount_IsRejected()
    {
        Assert.False(
            TrustedIdentityAccessContract.IsActiveAccountOwner(
                Guid.NewGuid().ToString("D"), null));
    }
}
