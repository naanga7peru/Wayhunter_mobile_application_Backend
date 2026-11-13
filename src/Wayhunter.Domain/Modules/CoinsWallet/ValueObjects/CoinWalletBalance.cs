
namespace Wayhunter.Domain.Modules.CoinsWallet.ValueObjects;

public sealed record CoinWalletBalance
{
    public long PostedCoins { get; }
    public long ReservedCoins { get; }
    public long LifetimeEarnedCoins { get; }

    public long SpendableCoins => PostedCoins - ReservedCoins;

    public CoinWalletBalance(
        long postedCoins,
        long reservedCoins,
        long lifetimeEarnedCoins)
    {
        if (postedCoins < 0)
            throw new ArgumentOutOfRangeException(
                nameof(postedCoins));

        if (reservedCoins < 0)
            throw new ArgumentOutOfRangeException(
                nameof(reservedCoins));

        if (lifetimeEarnedCoins < 0)
            throw new ArgumentOutOfRangeException(
                nameof(lifetimeEarnedCoins));

        if (reservedCoins > postedCoins)
            throw new ArgumentException(
                "Reserved Coins cannot exceed Posted Coins.",
                nameof(reservedCoins));

        PostedCoins = postedCoins;
        ReservedCoins = reservedCoins;
        LifetimeEarnedCoins = lifetimeEarnedCoins;
    }
}
