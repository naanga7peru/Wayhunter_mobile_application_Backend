
using Microsoft.AspNetCore.Identity;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Wayhunter.Domain.Modules.IdentitySocial.Enums;

namespace Wayhunter.Api.Services.IdentitySocial;

public sealed class LoginCredentialValidationService
{
    private readonly IPasswordHasher<object> _hasher;

    public LoginCredentialValidationService(
        IPasswordHasher<object> hasher)
    {
        _hasher = hasher ??
            throw new ArgumentNullException(nameof(hasher));
    }

    public bool Validate(
        UserAccount? account,
        string? password)
    {
        if (account is null ||
            string.IsNullOrEmpty(password))
        {
            return false;
        }

        if (account.Status != UserAccountStatus.Active ||
            !account.VerifiedAtUtc.HasValue)
        {
            return false;
        }

        var verifiedAt = account.VerifiedAtUtc.Value;

        if (verifiedAt == default ||
            verifiedAt.Offset != TimeSpan.Zero ||
            verifiedAt < account.CreatedAtUtc)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(account.PasswordHash))
        {
            return false;
        }

        var result = _hasher.VerifyHashedPassword(
            new object(),
            account.PasswordHash,
            password);

        return result != PasswordVerificationResult.Failed;
    }
}
