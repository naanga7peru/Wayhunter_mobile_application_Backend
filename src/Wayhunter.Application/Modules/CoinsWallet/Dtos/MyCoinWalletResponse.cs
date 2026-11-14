
namespace Wayhunter.Application.Modules.CoinsWallet.Dtos;

/// <summary>
/// Response returned to the authenticated wallet owner.
/// Only Coins balances are exposed.
/// </summary>
public sealed record MyCoinWalletResponse(
    long PostedCoins,
    long ReservedCoins,
    long SpendableCoins,
    long LifetimeEarnedCoins);
