
using System.Security.Claims;
using Wayhunter.Api.Contracts;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;

namespace Wayhunter.Api.Services.IdentitySocial;

public sealed class TrustedIdentityResolutionService
{
    private readonly ITrustedIdentityAccountReader _accounts;

    public TrustedIdentityResolutionService(
        ITrustedIdentityAccountReader accounts)
    {
        _accounts = accounts ??
            throw new ArgumentNullException(nameof(accounts));
    }

    public async Task<UserAccount?> ResolveActiveAccountAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(principal);

        cancellationToken.ThrowIfCancellationRequested();

        if (!TrustedIdentityAccessContract.TryResolveUserId(
                principal, out var userId))
        {
            return null;
        }

        var account = await _accounts.FindByUserIdAsync(
            userId, cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        if (!TrustedIdentityAccessContract.IsActiveAccountOwner(
                userId, account))
        {
            return null;
        }

        return account;
    }
}
