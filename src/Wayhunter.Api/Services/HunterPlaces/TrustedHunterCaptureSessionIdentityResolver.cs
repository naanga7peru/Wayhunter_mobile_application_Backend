
using System;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Api.Contracts;
using Wayhunter.Api.Services.IdentitySocial;
using Wayhunter.Application.Modules.HunterPlaces.Interfaces;

namespace Wayhunter.Api.Services.HunterPlaces;

/// <summary>
/// Proposed M1-to-M4 Hunter identity adapter.
///
/// The caller must supply a user ID obtained from trusted
/// backend authentication, never from a client request.
///
/// This adapter checks a trusted M1 account before mapping
/// its GUID-formatted UserId to the capture-session HunterId.
///
/// The same-identity mapping requires explicit team approval
/// before production registration.
/// </summary>
public sealed class TrustedHunterCaptureSessionIdentityResolver
    : IHunterCaptureSessionIdentityResolver
{
    private readonly ITrustedIdentityAccountReader _accounts;

    public TrustedHunterCaptureSessionIdentityResolver(
        ITrustedIdentityAccountReader accounts)
    {
        _accounts = accounts ??
            throw new ArgumentNullException(nameof(accounts));
    }

    public async Task<Guid?> ResolveSessionHunterIdAsync(
        string authenticatedUserId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Only canonical, nonempty GUID-formatted IDs.
        if (string.IsNullOrWhiteSpace(authenticatedUserId) ||
            !Guid.TryParseExact(
                authenticatedUserId,
                "D",
                out var parsedId) ||
            parsedId == Guid.Empty)
        {
            return null;
        }

        var canonicalUserId = parsedId.ToString("D");

        // The account must come from trusted backend data.
        var account = await _accounts.FindByUserIdAsync(
            canonicalUserId,
            cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        // Reuse the existing M1 active-account contract.
        if (!TrustedIdentityAccessContract.IsActiveAccountOwner(
                canonicalUserId,
                account))
        {
            return null;
        }

        // Proposed one-to-one identity mapping.
        // Do not register in production until approved.
        return parsedId;
    }
}
