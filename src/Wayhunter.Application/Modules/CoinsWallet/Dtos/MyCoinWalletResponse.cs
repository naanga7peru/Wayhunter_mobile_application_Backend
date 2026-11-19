
namespace Wayhunter.Application.Modules.CoinsWallet.Dtos;

/// <summary>
/// Response returned to the authenticated wallet owner.
/// Only Coins balances are exposed.
/// </summary>
public sealed record MyCoinWalletResponse(
    long PostedCoins,
    long ReservedCoins,
    long SpendableCoins,
    long LifetimeEarnedCoins)
{
    // The complete level-number formula remains unapproved. These
    // fields expose only the confirmed 2,000-Coin progress baseline.
    public long LifetimeEarnedProgressCoins { get; init; }
    public long LifetimeEarnedProgressTargetCoins { get; init; }
}
