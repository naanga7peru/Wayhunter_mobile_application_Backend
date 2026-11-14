
using System;
using System.Globalization;
using Wayhunter.Domain.Modules.CoinsWallet.Enums;
using Wayhunter.Domain.Modules.CoinsWallet.Rules;
using Wayhunter.Domain.Modules.CoinsWallet.ValueObjects;
using Xunit;

namespace Wayhunter.UnitTests.Modules.CoinsWallet;

public class CoinArithmeticPolicyTests
{
    [Theory]
    [InlineData(CoinRewardType.WelcomeVerified, 100L)]
    [InlineData(CoinRewardType.HunterApproved, 200L)]
    [InlineData(CoinRewardType.GameCreatorApproved, 100L)]
    [InlineData(CoinRewardType.GameWinnerApproved, 100L)]
    public void ApprovedReward_UsesCorrectAmount(
        CoinRewardType type,
        long coins)
    {
        var wallet = new CoinWalletBalance(0, 0, 0);

        var result = CoinArithmeticPolicy.ApplyApprovedReward(
            wallet, type, coins);

        Assert.Equal(coins, result.PostedCoins);
        Assert.Equal(coins, result.LifetimeEarnedCoins);
    }

    [Theory]
    [InlineData(100L)]
    [InlineData(-200L)]
    public void UnapprovedRewardAmount_IsRejected(long coins)
    {
        var wallet = new CoinWalletBalance(0, 0, 0);

        Assert.Throws<ArgumentException>(() =>
            CoinArithmeticPolicy.ApplyApprovedReward(
                wallet,
                CoinRewardType.HunterApproved,
                coins));

        Assert.Equal(0, wallet.PostedCoins);
    }

    [Fact]
    public void InvalidRewardType_IsRejected()
    {
        var wallet = new CoinWalletBalance(0, 0, 0);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CoinArithmeticPolicy.ApplyApprovedReward(
                wallet,
                (CoinRewardType)999,
                100));
    }

    [Fact]
    public void PostedBalanceOverflow_IsRejected()
    {
        var wallet = new CoinWalletBalance(
            long.MaxValue, 0, 100);

        Assert.Throws<OverflowException>(() =>
            CoinArithmeticPolicy.ApplyApprovedReward(
                wallet,
                CoinRewardType.HunterApproved,
                200));
    }

    [Fact]
    public void LifetimeEarnedOverflow_IsRejected()
    {
        var wallet = new CoinWalletBalance(
            1000, 0, long.MaxValue);

        Assert.Throws<OverflowException>(() =>
            CoinArithmeticPolicy.ApplyApprovedReward(
                wallet,
                CoinRewardType.HunterApproved,
                200));
    }

    [Fact]
    public void RewardCredit_PreservesReservedCoins()
    {
        var wallet = new CoinWalletBalance(
            10100, 10000, 10100);

        var result = CoinArithmeticPolicy.ApplyApprovedReward(
            wallet,
            CoinRewardType.HunterApproved,
            200);

        Assert.Equal(10300, result.PostedCoins);
        Assert.Equal(10000, result.ReservedCoins);
        Assert.Equal(300, result.SpendableCoins);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("1.5")]
    [InlineData("10000.01")]
    public void InvalidWholeCoins_AreRejected(string value)
    {
        decimal coins = decimal.Parse(
            value,
            CultureInfo.InvariantCulture);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CoinArithmeticPolicy.RequirePositiveWholeCoins(coins));
    }

    [Fact]
    public void CoinsOutsideLongRange_AreRejected()
    {
        decimal tooLarge = (decimal)long.MaxValue + 1m;

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CoinArithmeticPolicy.RequirePositiveWholeCoins(
                tooLarge));
    }

    [Fact]
    public void ValidWholeCoins_AreAccepted()
    {
        Assert.Equal(
            10000L,
            CoinArithmeticPolicy.RequirePositiveWholeCoins(
                10000m));
    }

    [Fact]
    public void ApprovedLkrReference_IsAccepted()
    {
        Assert.Equal(
            100m,
            CoinArithmeticPolicy
                .RequireSampleVoucherReferenceLkr(100m));
    }

    [Theory]
    [InlineData("99.99")]
    [InlineData("100.01")]
    public void UnapprovedLkrReference_IsRejected(
        string value)
    {
        decimal amount = decimal.Parse(
            value,
            CultureInfo.InvariantCulture);

        Assert.Throws<ArgumentException>(() =>
            CoinArithmeticPolicy
                .RequireSampleVoucherReferenceLkr(amount));
    }

    [Fact]
    public void InsufficientCoins_CannotReserveVoucher()
    {
        var wallet = new CoinWalletBalance(
            10099, 0, 10099);

        Assert.Throws<InvalidOperationException>(() =>
            CoinArithmeticPolicy.ReserveSampleVoucher(wallet));

        Assert.Equal(0, wallet.ReservedCoins);
    }

    [Fact]
    public void EligibleWallet_CanReserveVoucher()
    {
        var wallet = new CoinWalletBalance(
            10100, 0, 10100);

        var result =
            CoinArithmeticPolicy.ReserveSampleVoucher(wallet);

        Assert.Equal(10100, result.PostedCoins);
        Assert.Equal(10000, result.ReservedCoins);
        Assert.Equal(100, result.SpendableCoins);
    }

    [Fact]
    public void ApprovedVoucher_ConsumesReservation()
    {
        var wallet = new CoinWalletBalance(
            10100, 10000, 20000);

        var result =
            CoinArithmeticPolicy.ApproveReservedVoucher(wallet);

        Assert.Equal(100, result.PostedCoins);
        Assert.Equal(0, result.ReservedCoins);
        Assert.Equal(100, result.SpendableCoins);
        Assert.Equal(20000, result.LifetimeEarnedCoins);
    }

    [Fact]
    public void VoucherWithoutReservation_IsRejected()
    {
        var wallet = new CoinWalletBalance(
            10100, 0, 10100);

        Assert.Throws<InvalidOperationException>(() =>
            CoinArithmeticPolicy.ApproveReservedVoucher(wallet));
    }
}
