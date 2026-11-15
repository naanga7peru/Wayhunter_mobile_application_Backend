
using Microsoft.AspNetCore.Identity;
using Wayhunter.Api.Services.IdentitySocial;
using Wayhunter.Application.Modules.IdentitySocial.Services;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Xunit;

namespace Wayhunter.UnitTests.Modules.IdentitySocial;

public sealed class LoginAuthenticationServiceTests
{
    private const string Password = "StrongPassword123!";

    private static readonly DateTimeOffset Now =
        new(2026, 11, 15, 10, 0, 0, TimeSpan.Zero);

    private static readonly LoginLockoutOptions Options =
        new(5, TimeSpan.FromMinutes(15));

    private sealed class FakeReader : ILoginAccountReader
    {
        public UserAccount? Account { get; set; }

        public int Calls { get; private set; }

        public Task<UserAccount?> FindByNormalizedEmailAsync(
            string normalizedEmail,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;
            return Task.FromResult(Account);
        }
    }

    private static UserAccount CreateAccount(
        bool activate = true)
    {
        var hasher = new PasswordHasher<object>();

        var hash = hasher.HashPassword(
            new object(),
            Password);

        var account = UserAccount.RegisterPending(
            "Wayhunter Traveler",
            "traveler@example.com",
            hash,
            Now);

        if (activate)
            account.ActivateAfterVerifiedOtp(
                Now.AddMinutes(1));

        return account;
    }

    private static LoginAuthenticationService CreateService(
        FakeReader reader)
    {
        return new LoginAuthenticationService(
            new LoginAccountLookupService(reader),
            new LoginCredentialValidationService(
                new PasswordHasher<object>()));
    }

    private static LoginLockoutState EmptyState()
        => new(0, null);

    [Fact]
    public async Task ValidCredentials_Authenticate()
    {
        var account = CreateAccount();

        var service = CreateService(
            new FakeReader { Account = account });

        var result = await service.AuthenticateAsync(
            "traveler@example.com",
            Password,
            EmptyState(),
            Options,
            Now.AddMinutes(2));

        Assert.True(result.IsAuthenticated);
        Assert.False(result.IsLocked);
        Assert.Same(account, result.Account);
        Assert.Equal(0, result.NextLockoutState.FailedAttempts);
    }

    [Fact]
    public async Task WrongPassword_IncrementsFailures()
    {
        var reader = new FakeReader
        {
            Account = CreateAccount()
        };

        var result = await CreateService(reader).AuthenticateAsync(
            "traveler@example.com",
            "WrongPassword",
            EmptyState(),
            Options,
            Now.AddMinutes(2));

        Assert.False(result.IsAuthenticated);
        Assert.Null(result.Account);
        Assert.Equal(1, result.NextLockoutState.FailedAttempts);
    }

    [Fact]
    public async Task FifthFailure_TriggersLockout()
    {
        var reader = new FakeReader
        {
            Account = CreateAccount()
        };

        var result = await CreateService(reader).AuthenticateAsync(
            "traveler@example.com",
            "WrongPassword",
            new LoginLockoutState(4, null),
            Options,
            Now.AddMinutes(2));

        Assert.False(result.IsAuthenticated);
        Assert.True(result.IsLocked);
        Assert.Equal(
            TimeSpan.FromMinutes(15),
            result.RetryAfter);
        Assert.Equal(5, result.NextLockoutState.FailedAttempts);
    }

    [Fact]
    public async Task LockedAccount_BlocksAuthenticationBeforeLookup()
    {
        var reader = new FakeReader
        {
            Account = CreateAccount()
        };

        var current = new LoginLockoutState(
            5,
            Now.AddMinutes(15));

        var result = await CreateService(reader).AuthenticateAsync(
            "traveler@example.com",
            Password,
            current,
            Options,
            Now.AddMinutes(5));

        Assert.False(result.IsAuthenticated);
        Assert.True(result.IsLocked);
        Assert.Equal(0, reader.Calls);
        Assert.Equal(current, result.NextLockoutState);
    }

    [Fact]
    public async Task ExpiredLockout_AllowsLoginAndResetsState()
    {
        var reader = new FakeReader
        {
            Account = CreateAccount()
        };

        var result = await CreateService(reader).AuthenticateAsync(
            "traveler@example.com",
            Password,
            new LoginLockoutState(5, Now.AddMinutes(15)),
            Options,
            Now.AddMinutes(16));

        Assert.True(result.IsAuthenticated);
        Assert.Equal(0, result.NextLockoutState.FailedAttempts);
        Assert.Null(result.NextLockoutState.LockedUntilUtc);
    }

    [Fact]
    public async Task PendingOtpAccount_CannotLogin()
    {
        var reader = new FakeReader
        {
            Account = CreateAccount(activate: false)
        };

        var result = await CreateService(reader).AuthenticateAsync(
            "traveler@example.com",
            Password,
            EmptyState(),
            Options,
            Now.AddMinutes(2));

        Assert.False(result.IsAuthenticated);
        Assert.Null(result.Account);
    }

    [Fact]
    public async Task SuspendedAccount_CannotLogin()
    {
        var account = CreateAccount();
        account.Suspend();

        var result = await CreateService(
            new FakeReader { Account = account })
            .AuthenticateAsync(
                "traveler@example.com",
                Password,
                EmptyState(),
                Options,
                Now.AddMinutes(2));

        Assert.False(result.IsAuthenticated);
        Assert.Null(result.Account);
    }

    [Fact]
    public async Task MissingAccount_IsRejected()
    {
        var result = await CreateService(
            new FakeReader())
            .AuthenticateAsync(
                "missing@example.com",
                Password,
                EmptyState(),
                Options,
                Now.AddMinutes(2));

        Assert.False(result.IsAuthenticated);
        Assert.Null(result.Account);
    }

    [Fact]
    public async Task Success_ResetsPreviousFailures()
    {
        var result = await CreateService(
            new FakeReader { Account = CreateAccount() })
            .AuthenticateAsync(
                "traveler@example.com",
                Password,
                new LoginLockoutState(3, null),
                Options,
                Now.AddMinutes(2));

        Assert.True(result.IsAuthenticated);
        Assert.Equal(0, result.NextLockoutState.FailedAttempts);
    }

    [Fact]
    public async Task CancelledRequest_IsRejected()
    {
        var reader = new FakeReader();

        using var cancellation =
            new CancellationTokenSource();

        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => CreateService(reader).AuthenticateAsync(
                "traveler@example.com",
                Password,
                EmptyState(),
                Options,
                Now.AddMinutes(2),
                cancellation.Token));

        Assert.Equal(0, reader.Calls);
    }

    [Fact]
    public async Task InvalidOptions_AreRejected()
    {
        var service = CreateService(new FakeReader());

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => service.AuthenticateAsync(
                "traveler@example.com",
                Password,
                EmptyState(),
                new LoginLockoutOptions(0, TimeSpan.Zero),
                Now.AddMinutes(2)));
    }

    [Fact]
    public void NullDependencies_AreRejected()
    {
        var reader = new FakeReader();
        var lookup = new LoginAccountLookupService(reader);
        var validator = new LoginCredentialValidationService(
            new PasswordHasher<object>());

        Assert.Throws<ArgumentNullException>(() =>
            new LoginAuthenticationService(null!, validator));

        Assert.Throws<ArgumentNullException>(() =>
            new LoginAuthenticationService(lookup, null!));
    }
}
