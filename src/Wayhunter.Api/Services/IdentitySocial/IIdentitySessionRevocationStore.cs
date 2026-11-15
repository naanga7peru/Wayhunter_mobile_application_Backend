
using Wayhunter.Domain.Modules.IdentitySocial.Entities;

namespace Wayhunter.Api.Services.IdentitySocial;

public enum SessionRevocationWriteResult
{
    Revoked,
    AlreadyRevoked,
    NotFound
}

public interface IIdentitySessionRevocationStore
{
    Task<IdentitySession?> FindAsync(
        string sessionId,
        CancellationToken cancellationToken = default);

    // Must atomically verify session ownership and
    // persist revocation on the server.
    //
    // Never trust UserId supplied by the client.
    // The service must obtain it from trusted identity.
    Task<SessionRevocationWriteResult> TryRevokeAsync(
        string userId,
        string sessionId,
        DateTimeOffset revokedAtUtc,
        CancellationToken cancellationToken = default);
}
