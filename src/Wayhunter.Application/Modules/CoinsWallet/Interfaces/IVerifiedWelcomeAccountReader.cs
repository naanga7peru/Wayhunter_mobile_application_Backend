
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;

namespace Wayhunter.Application.Modules.CoinsWallet.Interfaces;

/// <summary>
/// Reads an account from trusted M1 identity storage.
/// Never use client-provided verification flags.
/// </summary>
public interface IVerifiedWelcomeAccountReader
{
    Task<UserAccount?> FindByUserIdAsync(
        string userId,
        CancellationToken cancellationToken = default);
}
