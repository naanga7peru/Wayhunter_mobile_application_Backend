
using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.CoinsWallet.Dtos;
using Wayhunter.Application.Modules.CoinsWallet.Interfaces;

namespace Wayhunter.Application.Modules.CoinsWallet.Queries;

public sealed class GetMyCoinWalletService
{
    private readonly ICoinWalletReader _reader;

    public GetMyCoinWalletService(ICoinWalletReader reader)
    {
        _reader = reader ??
            throw new ArgumentNullException(nameof(reader));
    }

    public async Task<MyCoinWalletResponse?> GetMineAsync(
        ClaimsPrincipal? currentUser,
        CancellationToken cancellationToken = default)
    {
        // Reject unauthenticated users.
        if (currentUser?.Identity?.IsAuthenticated != true)
        {
            throw new UnauthorizedAccessException(
                "Authentication required.");
        }

        // Never accept the wallet owner ID directly from the client.
        // Obtain it from authenticated claims only.
        string? ownerId = currentUser
            .FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrWhiteSpace(ownerId))
        {
            ownerId = currentUser.FindFirst("sub")?.Value;
        }

        if (string.IsNullOrWhiteSpace(ownerId))
        {
            throw new UnauthorizedAccessException(
                "Authenticated user ID required.");
        }

        ownerId = ownerId.Trim();

        // Request only the authenticated user's wallet.
        CoinWalletSnapshot? snapshot =
            await _reader.GetByOwnerUserIdAsync(
                ownerId,
                cancellationToken);

        if (snapshot is null)
        {
            return null;
        }

        // Fail closed if repository returns another user's wallet.
        if (!string.Equals(
            snapshot.OwnerUserId,
            ownerId,
            StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Wallet ownership mismatch.");
        }

        var balance = snapshot.Balance;

        return new MyCoinWalletResponse(
            balance.PostedCoins,
            balance.ReservedCoins,
            balance.SpendableCoins,
            balance.LifetimeEarnedCoins);
    }
}
