
using Microsoft.AspNetCore.Identity;
using Wayhunter.Api.Services.IdentitySocial;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Xunit;

namespace Wayhunter.UnitTests.Modules.IdentitySocial;

public sealed class LoginCredentialValidationServiceTests
{
    private const string Password = "StrongPassword123!";

    private static readonly DateTimeOffset Now =
        new(2026, 11, 15, 10, 0, 0, TimeSpan.Zero);

    private readonly PasswordHasher<object> _hasher = new();

    private LoginCredentialValidationService Service()
        => new(_hasher);

    private UserAccount CreateAccount(bool activate = true)
    {
        var hash = _hasher.HashPassword(
            new object(), Password);

        var account = UserAccount.RegisterPending(
            "Test Traveler",
            $"user-{Guid.NewGuid():N}@example.com",
            hash,
            Now);

        if (activate)
            account.ActivateAfterVerifiedOtp(
                Now.AddMinutes(5));

        return account;
    }

    [Fact]
    public void CorrectPassword_ActiveUser_IsAccepted()
    {
        var account = CreateAccount();

        Assert.True(Service().Validate(account, Password));
    }

    [Fact]
    public void WrongPassword_IsRejected()
    {
        var account = CreateAccount();

        Assert.False(
            Service().Validate(account, "WrongPassword123!"));
    }

    [Fact]
    public void PendingOtpUser_IsRejected()
    {
        var account = CreateAccount(false);

        Assert.False(Service().Validate(account, Password));
    }

    [Fact]
    public void SuspendedAccount_IsRejected()
    {
        var account = CreateAccount();
        account.Suspend();

        Assert.False(Service().Validate(account, Password));
    }

    [Fact]
    public void DeletionRequestedUser_IsRejected()
    {
        var account = CreateAccount();
        account.RequestDeletion(Now.AddMinutes(10));

        Assert.False(Service().Validate(account, Password));
    }

    [Fact]
    public void MissingAccount_IsRejected()
    {
        Assert.False(
            Service().Validate(null, Password));
    }

    [Fact]
    public void EmptyPassword_IsRejected()
    {
        var account = CreateAccount();

        Assert.False(Service().Validate(account, ""));
    }

    [Fact]
    public void NullHasher_IsRejected()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new LoginCredentialValidationService(null!));
    }
}
