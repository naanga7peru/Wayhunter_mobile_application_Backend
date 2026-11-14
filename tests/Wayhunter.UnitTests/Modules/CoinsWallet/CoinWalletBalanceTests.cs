
using Wayhunter.Domain.Modules.CoinsWallet.ValueObjects;
using Xunit;

namespace Wayhunter.UnitTests.Modules.CoinsWallet;

public class CoinWalletBalanceTests
{
    [Theory]
    [InlineData(0, 0, 0, 0)]
    [InlineData(500, 100, 800, 400)]
    [InlineData(10100, 10000, 12000, 100)]
    [InlineData(100, 0, 2500, 100)]
    public void SpendableCoins_IsCalculatedCorrectly(
        long posted,
        long reserved,
        long earned,
        long expected)
    {
        var balance = new CoinWalletBalance(
            posted, reserved, earned);

        Assert.Equal(expected, balance.SpendableCoins);
        Assert.Equal(earned, balance.LifetimeEarnedCoins);
    }

    [Fact]
    public void NegativePostedCoins_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new CoinWalletBalance(-1, 0, 0));
    }

    [Fact]
    public void NegativeReservedCoins_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new CoinWalletBalance(100, -1, 100));
    }

    [Fact]
    public void NegativeLifetimeEarnedCoins_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new CoinWalletBalance(100, 0, -1));
    }

    [Fact]
    public void ReservationExceedingBalance_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => new CoinWalletBalance(500, 600, 1000));
    }

    [Fact]
    public void LifetimeEarned_IsNotReducedByReservation()
    {
        var balance = new CoinWalletBalance(
            10100, 10000, 12000);

        Assert.Equal(100, balance.SpendableCoins);
        Assert.Equal(12000, balance.LifetimeEarnedCoins);
    }
}
