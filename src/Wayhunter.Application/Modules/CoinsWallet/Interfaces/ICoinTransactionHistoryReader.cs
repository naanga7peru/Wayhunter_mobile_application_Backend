using Wayhunter.Application.Modules.CoinsWallet.Dtos;
using Wayhunter.Domain.Modules.CoinsWallet.Enums;

namespace Wayhunter.Application.Modules.CoinsWallet.Interfaces;

public sealed record CoinTransactionHistoryQuery(
    string OwnerUserId,
    int Page,
    int PageSize,
    CoinTransactionKind? Kind = null,
    CoinRewardType? RewardType = null);

public interface ICoinTransactionHistoryReader
{
    Task<CoinTransactionHistoryResponse> GetAsync(
        CoinTransactionHistoryQuery query,
        CancellationToken cancellationToken = default);
}
