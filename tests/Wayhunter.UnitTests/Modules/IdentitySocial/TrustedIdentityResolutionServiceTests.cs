
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Api.Services.IdentitySocial;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Xunit;

namespace Wayhunter.UnitTests.Modules.IdentitySocial;

public sealed class TrustedIdentityResolutionServiceTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 11, 15, 10, 0, 0, TimeSpan.Zero);

    private static UserAccount CreateAccount(
        bool active = true)
    {
        var account = UserAccount.RegisterPending(
            "Test Hunter",
            $"hunter-{Guid.NewGuid():N}@example.com",
            "test-only-password-hash",
            Now);

        if (active)
            account.ActivateAfterVerifiedOtp(
                Now.AddMinutes(5));

        return account;
    }

    private static ClaimsPrincipal Principal(
        string? userId,
        bool authenticated = true)
    {
        var claims = new List<Claim>();

        if (userId is not null)
            claims.Add(new Claim(
                ClaimTypes.NameIdentifier, userId));

        return new ClaimsPrincipal(
            new ClaimsIdentity(
                claims,
                authenticated ? "TestAuth" : null));
    }

    private sealed class FakeReader
        : ITrustedIdentityAccountReader
    {
        public UserAccount? Account { get; set; }
        public int Calls { get; private set; }

        public Task<UserAccount?> FindByUserIdAsync(
            string userId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;

            return Task.FromResult(Account);
        }
    }

    [Fact]
    public async Task ActiveVerifiedUser_IsResolved()
    {
        var account = CreateAccount();
        var reader = new FakeReader { Account = account };
        var service = new TrustedIdentityResolutionService(reader);

        var result = await service.ResolveActiveAccountAsync(
            Principal(account.UserId));

        Assert.Same(account, result);
        Assert.Equal(1, reader.Calls);
    }

    [Fact]
    public async Task AnonymousUser_IsRejectedWithoutLookup()
    {
        var account = CreateAccount();
        var reader = new FakeReader { Account = account };
        var service = new TrustedIdentityResolutionService(reader);

        var result = await service.ResolveActiveAccountAsync(
            Principal(account.UserId, false));

        Assert.Null(result);
        Assert.Equal(0, reader.Calls);
    }

    [Fact]
    public async Task InvalidGuid_IsRejectedWithoutLookup()
    {
        var reader = new FakeReader();
        var service = new TrustedIdentityResolutionService(reader);

        var result = await service.ResolveActiveAccountAsync(
            Principal("invalid-guid"));

        Assert.Null(result);
        Assert.Equal(0, reader.Calls);
    }

    [Fact]
    public async Task MissingAccount_IsRejected()
    {
        var reader = new FakeReader();
        var service = new TrustedIdentityResolutionService(reader);

        var result = await service.ResolveActiveAccountAsync(
            Principal(Guid.NewGuid().ToString("D")));

        Assert.Null(result);
        Assert.Equal(1, reader.Calls);
    }

    [Fact]
    public async Task WrongAccountOwner_IsRejected()
    {
        var account = CreateAccount();
        var reader = new FakeReader { Account = account };
        var service = new TrustedIdentityResolutionService(reader);

        var result = await service.ResolveActiveAccountAsync(
            Principal(Guid.NewGuid().ToString("D")));

        Assert.Null(result);
    }

    [Fact]
    public async Task PendingAccount_IsRejected()
    {
        var account = CreateAccount(false);
        var reader = new FakeReader { Account = account };
        var service = new TrustedIdentityResolutionService(reader);

        var result = await service.ResolveActiveAccountAsync(
            Principal(account.UserId));

        Assert.Null(result);
    }

    [Fact]
    public async Task SuspendedAccount_IsRejected()
    {
        var account = CreateAccount();
        account.Suspend();

        var reader = new FakeReader { Account = account };
        var service = new TrustedIdentityResolutionService(reader);

        var result = await service.ResolveActiveAccountAsync(
            Principal(account.UserId));

        Assert.Null(result);
    }

    [Fact]
    public async Task DeletionRequestedAccount_IsRejected()
    {
        var account = CreateAccount();
        account.RequestDeletion(Now.AddMinutes(10));
        var reader = new FakeReader { Account = account };
        var service = new TrustedIdentityResolutionService(reader);

        var result = await service.ResolveActiveAccountAsync(
            Principal(account.UserId));

        Assert.Null(result);
    }

    [Fact]
    public async Task ConflictingClaims_AreRejectedWithoutLookup()
    {
        var reader = new FakeReader();
        var service = new TrustedIdentityResolutionService(reader);

        var claims = new[]
        {
            new Claim(
                ClaimTypes.NameIdentifier,
                Guid.NewGuid().ToString("D")),
            new Claim(
                "sub",
                Guid.NewGuid().ToString("D"))
        };

        var principal = new ClaimsPrincipal(
            new ClaimsIdentity(claims, "TestAuth"));

        var result = await service.ResolveActiveAccountAsync(
            principal);

        Assert.Null(result);
        Assert.Equal(0, reader.Calls);
    }

    [Fact]
    public async Task CancelledOperation_DoesNotReadAccount()
    {
        var reader = new FakeReader();
        var service = new TrustedIdentityResolutionService(reader);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.ResolveActiveAccountAsync(
                Principal(Guid.NewGuid().ToString("D")),
                cts.Token));

        Assert.Equal(0, reader.Calls);
    }

    [Fact]
    public void NullReader_IsRejected()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new TrustedIdentityResolutionService(null!));
    }
}
