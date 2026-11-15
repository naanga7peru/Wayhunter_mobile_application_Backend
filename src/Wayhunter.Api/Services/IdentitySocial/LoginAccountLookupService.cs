
using System.Net.Mail;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;

namespace Wayhunter.Api.Services.IdentitySocial;

public sealed class LoginAccountLookupService
{
    private readonly ILoginAccountReader _accounts;

    public LoginAccountLookupService(
        ILoginAccountReader accounts)
    {
        _accounts = accounts ??
            throw new ArgumentNullException(nameof(accounts));
    }

    public async Task<UserAccount?> FindByEmailAsync(
        string? email,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(email))
            return null;

        var trimmedEmail = email.Trim();

        if (trimmedEmail.Length > 320 ||
            !MailAddress.TryCreate(
                trimmedEmail, out var parsedEmail) ||
            !string.Equals(
                parsedEmail.Address,
                trimmedEmail,
                StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        // Match the existing M1 registration email format.
        var normalizedEmail =
            trimmedEmail.ToLowerInvariant();

        var account = await _accounts
            .FindByNormalizedEmailAsync(
                normalizedEmail,
                cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        if (account is null)
            return null;

        // Never trust a reader returning another account.
        if (!string.Equals(
                account.NormalizedEmail,
                normalizedEmail,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Login account lookup ownership mismatch.");
        }

        return account;
    }
}
