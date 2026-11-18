
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Wayhunter.Application.Modules.CoinsWallet.Interfaces;
using Wayhunter.Domain.Modules.CoinsWallet.ValueObjects;
using Wayhunter.Infrastructure.Persistence.DbContexts;

namespace Wayhunter.Infrastructure.Persistence.Repositories.CoinsWallet;

public sealed class SqlCoinWalletReader : ICoinWalletReader
{
    private readonly WayhunterSharedDbContext _db;

    public SqlCoinWalletReader(WayhunterSharedDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<CoinWalletSnapshot?> GetByOwnerUserIdAsync(
        string ownerUserId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ownerUserId))
        {
            throw new ArgumentException(
                "Wallet owner is required.",
                nameof(ownerUserId));
        }

        var normalizedId = ownerUserId.Trim();

        var wallet = await _db.CoinWallets
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.UserId == normalizedId,
                cancellationToken);

        if (wallet is null)
        {
            return null;
        }

        return new CoinWalletSnapshot(
            wallet.UserId,
            new CoinWalletBalance(
                wallet.PostedCoins,
                wallet.ReservedCoins,
                wallet.LifetimeEarnedCoins));
    }
}
