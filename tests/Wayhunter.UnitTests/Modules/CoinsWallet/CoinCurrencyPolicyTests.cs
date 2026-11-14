
using System;
using Wayhunter.Domain.Modules.CoinsWallet.Rules;
using Xunit;

namespace Wayhunter.UnitTests.Modules.CoinsWallet;

public class CoinCurrencyPolicyTests
{
    [Fact]
    public void CoinsCurrency_IsSupported()
    {
        Assert.True(
            CoinCurrencyPolicy.IsSupported("Coins"));
    }

    [Fact]
    public void CoinsValidation_DoesNotThrow()
    {
        CoinCurrencyPolicy.EnsureSupported("Coins");
    }

    [Theory]
    [InlineData("HP")]
    [InlineData("RC")]
    [InlineData("XP")]
    [InlineData("coins")]
    [InlineData("")]
    [InlineData(" ")]
    public void UnsupportedCurrency_IsRejected(
        string currency)
    {
        Assert.False(
            CoinCurrencyPolicy.IsSupported(currency));

        Assert.Throws<ArgumentException>(() =>
            CoinCurrencyPolicy.EnsureSupported(currency));
    }

    [Fact]
    public void NullCurrency_IsRejected()
    {
        Assert.False(
            CoinCurrencyPolicy.IsSupported(null));

        Assert.Throws<ArgumentException>(() =>
            CoinCurrencyPolicy.EnsureSupported(null));
    }
}
