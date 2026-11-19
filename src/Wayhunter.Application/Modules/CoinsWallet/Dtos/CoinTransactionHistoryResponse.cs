using Wayhunter.Domain.Modules.CoinsWallet.Enums;

namespace Wayhunter.Application.Modules.CoinsWallet.Dtos;

public enum CoinTransactionKind
{
    Reward = 1,
    Hold = 2,
    Debit = 3,
    Release = 4,
    Correction = 5
}

public sealed record CoinTransactionResponse(
    Guid TransactionId,
    CoinTransactionKind Kind,
    long CoinsAmount,
    CoinRewardType? RewardType,
    string SourceRecordId,
    string Status,
    DateTimeOffset RecordedAtUtc,
    Guid? CorrectionOfEntryId);

public sealed record CoinTransactionHistoryResponse(
    int Page,
    int PageSize,
    int TotalCount,
    IReadOnlyList<CoinTransactionResponse> Items);
