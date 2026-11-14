
using Wayhunter.Domain.Modules.CoinsWallet.Rules;
using Wayhunter.Domain.Modules.CoinsWallet.ValueObjects;
using Xunit;

namespace Wayhunter.UnitTests.Modules.CoinsWallet;

public class CoinLevelRedemptionRulesTests
{
    [Theory]
    [InlineData(0, 0, false)]
    [InlineData(1999, 1999, false)]
    [InlineData(2000, 2000, true)]
    [InlineData(2001, 2000, true)]
    public void InitialLevelProgress_IsCorrect(
        long earned,
        long expectedProgress,
        bool expectedReached)
    {
        Assert.Equal(
            expectedProgress,
            CoinLevelRedemptionRules
                .GetInitialLevelBarProgress(earned));

        Assert.Equal(
            expectedReached,
            CoinLevelRedemptionRules
                .HasReachedInitialThreshold(earned));
    }

    [Theory]
    [InlineData(10000, false)]
    [InlineData(10099, false)]
    [InlineData(10100, true)]
    [InlineData(10101, true)]
    public void RedemptionThreshold_IsCorrect(
        long spendable,
        bool expected)
    {
        var wallet = new CoinWalletBalance(
            spendable, 0, spendable);

        Assert.Equal(
            expected,
            CoinLevelRedemptionRules
                .CanRequestSampleVoucher(wallet));
    }

    [Fact]
    public void ReservedCoins_ReduceEligibility()
    {
        var wallet = new CoinWalletBalance(
            20100, 10100, 25000);

        Assert.False(
            CoinLevelRedemptionRules
                .CanRequestSampleVoucher(wallet));
    }

    [Fact]
    public void LifetimeEarnings_DoNotOverrideSpendable()
    {
        var wallet = new CoinWalletBalance(
            500, 0, 20000);

        Assert.False(
            CoinLevelRedemptionRules
                .CanRequestSampleVoucher(wallet));

        Assert.True(
            CoinLevelRedemptionRules
                .HasReachedInitialThreshold(
                    wallet.LifetimeEarnedCoins));
    }

    [Fact]
    public void NegativeLifetimeEarnings_AreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CoinLevelRedemptionRules
                .GetInitialLevelBarProgress(-1));
    }
}
