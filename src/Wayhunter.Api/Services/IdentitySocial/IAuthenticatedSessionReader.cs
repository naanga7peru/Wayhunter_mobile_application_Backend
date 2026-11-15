
using Wayhunter.Domain.Modules.IdentitySocial.Entities;

namespace Wayhunter.Api.Services.IdentitySocial;

public interface IAuthenticatedSessionReader
{
    Task<UserAccount?> FindAccountAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task<IdentitySession?> FindSessionAsync(
        string sessionId,
        CancellationToken cancellationToken = default);
}
