
using Microsoft.AspNetCore.Identity;
using Wayhunter.Api.Services.IdentitySocial;
using Wayhunter.Domain.Modules.IdentitySocial.Enums;
using Xunit;

namespace Wayhunter.UnitTests.Modules.IdentitySocial;

public sealed class RegistrationPreparationServiceTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 11, 15, 8, 0, 0, TimeSpan.Zero);

    private readonly PasswordHasher<object> _hasher = new();

    private RegistrationPreparationService CreateService()
        => new(_hasher);

    private PreparedRegistration Prepare(
        string name = "Demo Traveler",
        string email = "demo@example.com",
        string password = "StrongPassword123!",
        string confirm = "StrongPassword123!")
    {
        return CreateService().Prepare(
            name, email, password, confirm, Now);
    }

    [Fact]
    public void ValidRegistration_CreatesPendingUser()
    {
        var result = Prepare();

        Assert.True(
            Guid.TryParse(result.Account.UserId, out _));

        Assert.Equal(
            UserAccountStatus.PendingVerification,
            result.Account.Status);

        Assert.Equal(
            "demo@example.com",
            result.Account.NormalizedEmail);

        Assert.True(
            result.Response.RequiresOtpVerification);

        Assert.Equal(
            result.Account.UserId,
            result.Response.UserId);
    }

    [Fact]
    public void Password_IsSecurelyHashedAndVerifiable()
    {
        var result = Prepare();

        Assert.NotEqual(
            "StrongPassword123!",
            result.Account.PasswordHash);

        var verification = _hasher.VerifyHashedPassword(
            new object(),
            result.Account.PasswordHash,
            "StrongPassword123!");

        Assert.NotEqual(
            PasswordVerificationResult.Failed,
            verification);
    }

    [Fact]
    public void SamePassword_ProducesDifferentHashes()
    {
        var first = Prepare();
        var second = Prepare();

        Assert.NotEqual(
            first.Account.PasswordHash,
            second.Account.PasswordHash);
    }

    [Fact]
    public void InvalidEmail_IsRejected()
    {
        Assert.Throws<ArgumentException>(() =>
            Prepare(email: "not-an-email"));
    }

    [Fact]
    public void ShortPassword_IsRejected()
    {
        Assert.Throws<ArgumentException>(() =>
            Prepare(
                password: "abc",
                confirm: "abc"));
    }

    [Fact]
    public void PasswordMismatch_IsRejected()
    {
        Assert.Throws<ArgumentException>(() =>
            Prepare(confirm: "DifferentPassword123!"));
    }

    [Fact]
    public void EmptyName_IsRejected()
    {
        Assert.Throws<ArgumentException>(() =>
            Prepare(name: "   "));
    }
}
