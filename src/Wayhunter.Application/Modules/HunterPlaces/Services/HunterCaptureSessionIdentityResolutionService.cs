
using System;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.HunterPlaces.Interfaces;

namespace Wayhunter.Application.Modules.HunterPlaces.Services;

/// <summary>
/// Resolves a trusted authenticated account ID to
/// the Hunter ID required by the capture session store.
///
/// This service does not authenticate the caller.
/// The supplied account ID must come from trusted
/// backend authentication.
/// </summary>
public sealed class HunterCaptureSessionIdentityResolutionService
{
    private readonly IHunterCaptureSessionIdentityResolver _resolver;

    public HunterCaptureSessionIdentityResolutionService(
        IHunterCaptureSessionIdentityResolver resolver)
    {
        _resolver = resolver
            ?? throw new ArgumentNullException(nameof(resolver));
    }

    public async Task<Guid> ResolveRequiredAsync(
        string? authenticatedUserId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(authenticatedUserId))
        {
            throw new UnauthorizedAccessException(
                "Authenticated Hunter identity is required.");
        }

        var hunterId =
            await _resolver.ResolveSessionHunterIdAsync(
                authenticatedUserId,
                cancellationToken);

        if (!hunterId.HasValue || hunterId.Value == Guid.Empty)
        {
            throw new UnauthorizedAccessException(
                "Trusted Hunter identity mapping is unavailable.");
        }

        return hunterId.Value;
    }
}
