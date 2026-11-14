
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Domain.Modules.CoinsWallet.ValueObjects;

namespace Wayhunter.Application.Modules.CoinsWallet.Interfaces;

/// <summary>
/// Read-only wallet data linked to the wallet owner.
/// Database implementation will be added after schema approval.
/// </summary>
public sealed record CoinWalletSnapshot(
    string OwnerUserId,
    CoinWalletBalance Balance);

public interface ICoinWalletReader
{
    Task<CoinWalletSnapshot?> GetByOwnerUserIdAsync(
        string ownerUserId,
        CancellationToken cancellationToken = default);
}
