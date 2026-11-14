
using Wayhunter.Domain.Modules.CoinsWallet.ValueObjects;

namespace Wayhunter.Domain.Modules.CoinsWallet.Rules;

public static class CoinLevelRedemptionRules
{
    public const long InitialLevelBarCoins = 2000;

    public const long SampleVoucherCostCoins = 10000;

    public const long MinimumSpendableForRequestCoins = 10100;

    public static long GetInitialLevelBarProgress(
        long lifetimeEarnedCoins)
    {
        if (lifetimeEarnedCoins < 0)
            throw new ArgumentOutOfRangeException(
                nameof(lifetimeEarnedCoins));

        return Math.Min(
            lifetimeEarnedCoins,
            InitialLevelBarCoins);
    }

    public static bool HasReachedInitialThreshold(
        long lifetimeEarnedCoins)
    {
        if (lifetimeEarnedCoins < 0)
            throw new ArgumentOutOfRangeException(
                nameof(lifetimeEarnedCoins));

        return lifetimeEarnedCoins >= InitialLevelBarCoins;
    }

    public static bool CanRequestSampleVoucher(
        CoinWalletBalance wallet)
    {
        ArgumentNullException.ThrowIfNull(wallet);

        return wallet.SpendableCoins
            >= MinimumSpendableForRequestCoins;
    }
}
