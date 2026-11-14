
using System.Text.Json;
using Wayhunter.Application.Modules.IdentitySocial.Dtos;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Xunit;

namespace Wayhunter.UnitTests.Modules.IdentitySocial;

public sealed class RegistrationIdentityResponseTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 11, 14, 12, 0, 0, TimeSpan.Zero);

    private static UserAccount CreateAccount()
    {
        return UserAccount.RegisterPending(
            "Wayhunter Traveler",
            "traveler@example.com",
            "test-only-placeholder-hash",
            Now);
    }

    [Fact]
    public void Response_ShouldReturnServerGeneratedUserId()
    {
        var account = CreateAccount();

        var response =
            RegistrationIdentityResponseDto.FromPendingAccount(
                account);

        Assert.Equal(account.UserId, response.UserId);
        Assert.True(Guid.TryParse(response.UserId, out _));
        Assert.True(response.RequiresOtpVerification);
    }

    [Fact]
    public void DisplayNameEdit_ShouldKeepResponseUserId()
    {
        var account = CreateAccount();

        var before =
            RegistrationIdentityResponseDto.FromPendingAccount(
                account);

        account.UpdateDisplayName("New Traveler");

        var after =
            RegistrationIdentityResponseDto.FromPendingAccount(
                account);

        Assert.Equal(before.UserId, after.UserId);
        Assert.Equal("New Traveler", after.DisplayName);
    }

    [Fact]
    public void Response_ShouldNotExposeSensitiveFields()
    {
        var response =
            RegistrationIdentityResponseDto.FromPendingAccount(
                CreateAccount());

        using var document = JsonDocument.Parse(
            JsonSerializer.Serialize(response));

        var root = document.RootElement;

        Assert.True(root.TryGetProperty("UserId", out _));
        Assert.False(root.TryGetProperty("PasswordHash", out _));
        Assert.False(root.TryGetProperty("Email", out _));
    }

    [Fact]
    public void ActiveAccount_ShouldNotProducePendingResponse()
    {
        var account = CreateAccount();

        account.ActivateAfterVerifiedOtp(
            Now.AddMinutes(2));

        Assert.Throws<InvalidOperationException>(() =>
            RegistrationIdentityResponseDto.FromPendingAccount(
                account));
    }

    [Fact]
    public void NullAccount_ShouldBeRejected()
    {
        Assert.Throws<ArgumentNullException>(() =>
            RegistrationIdentityResponseDto.FromPendingAccount(
                null!));
    }
}
