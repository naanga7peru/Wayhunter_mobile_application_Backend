
using Wayhunter.Domain.Modules.IdentitySocial.Entities;

namespace Wayhunter.Api.Services.IdentitySocial;

public interface ITrustedIdentityAccountReader
{
    Task<UserAccount?> FindByUserIdAsync(
        string userId,
        CancellationToken cancellationToken = default);
}
