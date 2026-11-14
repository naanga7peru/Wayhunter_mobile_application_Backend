
using System;

namespace Wayhunter.Domain.Modules.CoinsWallet.Rules;

public static class CoinCurrencyPolicy
{
    public const string ActiveCurrency = "Coins";

    public static bool IsSupported(string? currency)
    {
        return string.Equals(
            currency,
            ActiveCurrency,
            StringComparison.Ordinal);
    }

    public static void EnsureSupported(string? currency)
    {
        if (!IsSupported(currency))
        {
            throw new ArgumentException(
                "Only Coins currency is supported. " +
                "Legacy HP, RC and XP conversion is not approved.",
                nameof(currency));
        }
    }
}
