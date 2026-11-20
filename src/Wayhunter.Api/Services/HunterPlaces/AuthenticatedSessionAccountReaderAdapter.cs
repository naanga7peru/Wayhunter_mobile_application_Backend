
using Wayhunter.Api.Services.IdentitySocial;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;

namespace Wayhunter.Api.Services.HunterPlaces;

/// <summary>
/// Reuses the existing M1 authenticated session reader
/// for M4 trusted account lookups.
/// Does not duplicate M1 SQL authentication logic.
/// </summary>
public sealed class AuthenticatedSessionAccountReaderAdapter
    : ITrustedIdentityAccountReader
{
    private readonly IAuthenticatedSessionReader _reader;

    public AuthenticatedSessionAccountReaderAdapter(
        IAuthenticatedSessionReader reader)
    {
        _reader = reader ??
            throw new ArgumentNullException(nameof(reader));
    }

    public Task<UserAccount?> FindByUserIdAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        return _reader.FindAccountAsync(
            userId,
            cancellationToken);
    }
}
