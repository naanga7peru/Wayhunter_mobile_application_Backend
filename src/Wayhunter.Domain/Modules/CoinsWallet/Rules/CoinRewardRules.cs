
using Wayhunter.Domain.Modules.CoinsWallet.Enums;

namespace Wayhunter.Domain.Modules.CoinsWallet.Rules;

public static class CoinRewardRules
{
    public static int GetAmount(CoinRewardType rewardType)
    {
        return rewardType switch
        {
            CoinRewardType.WelcomeVerified => 100,

            CoinRewardType.HunterApproved => 200,

            CoinRewardType.GameCreatorApproved => 100,

            CoinRewardType.GameWinnerApproved => 100,

            _ => throw new ArgumentOutOfRangeException(
                nameof(rewardType),
                rewardType,
                "Unsupported Coins reward type.")
        };
    }
}
