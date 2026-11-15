
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.CoinsWallet.Dtos;

namespace Wayhunter.Application.Modules.CoinsWallet.Interfaces;

/// <summary>
/// Authoritative reward ledger write boundary.
///
/// Production implementation MUST atomically enforce:
/// RecipientUserId + RewardType + SourceRecordId uniqueness.
///
/// A retry must return false without a second credit.
/// SQL implementation is pending shared migration approval.
/// </summary>
public interface ICoinRewardGrantStore
{
    Task<bool> TryGrantOnceAsync(
        CoinRewardGrantRequest request,
        CancellationToken cancellationToken = default);
}
