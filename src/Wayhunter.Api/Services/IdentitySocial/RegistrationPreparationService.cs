
using System.Net.Mail;
using Microsoft.AspNetCore.Identity;
using Wayhunter.Application.Modules.IdentitySocial.Dtos;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;

namespace Wayhunter.Api.Services.IdentitySocial;

public sealed record PreparedRegistration(
    UserAccount Account,
    RegistrationIdentityResponseDto Response);

public sealed class RegistrationPreparationService
{
    private readonly IPasswordHasher<object> _passwordHasher;

    public RegistrationPreparationService(
        IPasswordHasher<object> passwordHasher)
    {
        _passwordHasher = passwordHasher
            ?? throw new ArgumentNullException(
                nameof(passwordHasher));
    }

    public PreparedRegistration Prepare(
        string displayName,
        string email,
        string password,
        string confirmPassword,
        DateTimeOffset createdAtUtc)
    {
        if (string.IsNullOrWhiteSpace(displayName) ||
            displayName.Trim().Length > 200)
        {
            throw new ArgumentException(
                "Display name must be 1 to 200 characters.");
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException(
                "Email is required.");
        }

        var trimmedEmail = email.Trim();

        if (trimmedEmail.Length > 320 ||
            !MailAddress.TryCreate(
                trimmedEmail, out var parsedEmail) ||
            !string.Equals(
                parsedEmail.Address,
                trimmedEmail,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "Invalid email address.");
        }

        // Provisional policy; final password policy
        // requires product-owner confirmation.
        if (string.IsNullOrEmpty(password) ||
            password.Length < 8)
        {
            throw new ArgumentException(
                "Password must contain at least 8 characters.");
        }

        if (password != confirmPassword)
        {
            throw new ArgumentException(
                "Passwords do not match.");
        }

        if (createdAtUtc == default ||
            createdAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "Valid UTC creation time is required.");
        }

        // Standard ASP.NET Core Identity password hashing.
        // Never log or persist the plaintext password.
        var passwordHash = _passwordHasher.HashPassword(
            new object(),
            password);

        var account = UserAccount.RegisterPending(
            displayName,
            trimmedEmail,
            passwordHash,
            createdAtUtc);

        var response =
            RegistrationIdentityResponseDto
                .FromPendingAccount(account);

        // No database write or OTP is performed here.
        return new PreparedRegistration(
            account,
            response);
    }
}
