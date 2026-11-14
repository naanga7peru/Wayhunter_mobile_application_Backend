
using System;
using Wayhunter.Domain.Modules.CoinsWallet.Enums;
using Wayhunter.Domain.Modules.CoinsWallet.ValueObjects;

namespace Wayhunter.Domain.Modules.CoinsWallet.Rules;

/// <summary>
/// Exact Coins arithmetic validation.
/// Coins use long; sample voucher LKR uses decimal.
/// No database operations are performed here.
/// </summary>
public static class CoinArithmeticPolicy
{
    public static CoinWalletBalance ApplyApprovedReward(
        CoinWalletBalance wallet,
        CoinRewardType rewardType,
        long requestedCoins)
    {
        ArgumentNullException.ThrowIfNull(wallet);

        long approvedCoins =
            CoinRewardRules.GetAmount(rewardType);

        if (requestedCoins != approvedCoins)
        {
            throw new ArgumentException(
                "Unapproved Coins reward amount.",
                nameof(requestedCoins));
        }

        return new CoinWalletBalance(
            checked(wallet.PostedCoins + approvedCoins),
            wallet.ReservedCoins,
            checked(wallet.LifetimeEarnedCoins + approvedCoins));
    }

    public static long RequirePositiveWholeCoins(decimal coins)
    {
        if (coins <= 0 ||
            coins != decimal.Truncate(coins) ||
            coins > long.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(coins),
                "Coins must be positive whole numbers.");
        }

        return checked((long)coins);
    }

    public static decimal RequireSampleVoucherReferenceLkr(
        decimal referenceLkr)
    {
        if (referenceLkr != SampleVoucherPolicy.ReferenceValueLkr)
        {
            throw new ArgumentException(
                "Unapproved sample voucher LKR reference.",
                nameof(referenceLkr));
        }

        return referenceLkr;
    }

    public static CoinWalletBalance ReserveSampleVoucher(
        CoinWalletBalance wallet)
    {
        ArgumentNullException.ThrowIfNull(wallet);

        if (!CoinLevelRedemptionRules.CanRequestSampleVoucher(wallet))
        {
            throw new InvalidOperationException(
                "At least 10,100 spendable Coins required.");
        }

        long cost =
            CoinLevelRedemptionRules.SampleVoucherCostCoins;

        return new CoinWalletBalance(
            wallet.PostedCoins,
            checked(wallet.ReservedCoins + cost),
            wallet.LifetimeEarnedCoins);
    }

    public static CoinWalletBalance ApproveReservedVoucher(
        CoinWalletBalance wallet)
    {
        ArgumentNullException.ThrowIfNull(wallet);

        long cost =
            CoinLevelRedemptionRules.SampleVoucherCostCoins;

        if (wallet.ReservedCoins < cost ||
            wallet.PostedCoins < cost)
        {
            throw new InvalidOperationException(
                "Valid voucher reservation is required.");
        }

        // Balance projection only.
        // Request-specific idempotency requires shared persistence.
        return new CoinWalletBalance(
            checked(wallet.PostedCoins - cost),
            checked(wallet.ReservedCoins - cost),
            wallet.LifetimeEarnedCoins);
    }
}
