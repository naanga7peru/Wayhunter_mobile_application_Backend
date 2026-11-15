
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Wayhunter.Application.Modules.HunterPlaces.Interfaces;

/// <summary>
/// Defines a proposed trusted identity resolution boundary
/// for Hunter capture sessions.
///
/// The authenticated user ID must come from backend
/// authentication, never directly from a client request.
///
/// This contract does not define or approve an ID mapping.
/// </summary>
public interface IHunterCaptureSessionIdentityResolver
{
    /// <summary>
    /// Resolves an authenticated account ID to the
    /// Hunter ID expected by the current capture session store.
    ///
    /// Returns null when no trusted mapping is available.
    ///
    /// Implementations must use a team-approved identity
    /// strategy and must never generate a new random ID
    /// or trust a client-supplied Hunter ID.
    /// </summary>
    Task<Guid?> ResolveSessionHunterIdAsync(
        string authenticatedUserId,
        CancellationToken cancellationToken = default);
}
