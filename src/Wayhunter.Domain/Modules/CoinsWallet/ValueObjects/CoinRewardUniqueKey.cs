
using Wayhunter.Domain.Modules.CoinsWallet.Enums;
using Wayhunter.Domain.Modules.CoinsWallet.Rules;

namespace Wayhunter.Domain.Modules.CoinsWallet.ValueObjects;

public sealed record CoinRewardUniqueKey
{
    public string RecipientUserId { get; }
    public CoinRewardType RewardType { get; }
    public string SourceRecordId { get; }

    public CoinRewardUniqueKey(
        string recipientUserId,
        CoinRewardType rewardType,
        string sourceRecordId)
    {
        if (string.IsNullOrWhiteSpace(recipientUserId))
            throw new ArgumentException(
                "Recipient User ID is required.",
                nameof(recipientUserId));

        if (string.IsNullOrWhiteSpace(sourceRecordId))
            throw new ArgumentException(
                "Source Record ID is required.",
                nameof(sourceRecordId));

        // Reject unsupported reward types.
        _ = CoinRewardRules.GetAmount(rewardType);

        RecipientUserId = recipientUserId.Trim();
        RewardType = rewardType;
        SourceRecordId = sourceRecordId.Trim();
    }
}
