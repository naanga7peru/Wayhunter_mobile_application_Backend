using Microsoft.EntityFrameworkCore;
using Wayhunter.Application.Modules.CoinsWallet.Dtos;
using Wayhunter.Application.Modules.CoinsWallet.Interfaces;
using Wayhunter.Domain.Modules.CoinsWallet.Enums;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Wayhunter.Infrastructure.Persistence.Models.CoinsWallet;

namespace Wayhunter.Infrastructure.Persistence.Repositories.CoinsWallet;

public sealed class SqlCoinTransactionHistoryReader
    : ICoinTransactionHistoryReader
{
    private readonly WayhunterSharedDbContext _db;

    public SqlCoinTransactionHistoryReader(
        WayhunterSharedDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<CoinTransactionHistoryResponse> GetAsync(
        CoinTransactionHistoryQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        string ownerId = query.OwnerUserId.Trim();

        Guid? walletId = await _db.CoinWallets
            .AsNoTracking()
            .Where(x => x.UserId == ownerId)
            .Select(x => (Guid?)x.Id)
            .SingleOrDefaultAsync(cancellationToken);

        if (!walletId.HasValue)
        {
            return new CoinTransactionHistoryResponse(
                query.Page,
                query.PageSize,
                0,
                []);
        }

        IQueryable<CoinLedgerEntryRow> ledgerRows =
            _db.CoinLedgerEntries
                .AsNoTracking()
                .Where(x => x.WalletId == walletId.Value);

        if (query.RewardType.HasValue)
        {
            CoinRewardType rewardType = query.RewardType.Value;
            ledgerRows = ledgerRows.Where(
                x => x.RewardType == rewardType);
        }

        var ledger = await ledgerRows
            .Select(x => new CoinTransactionResponse(
                x.Id,
                x.CorrectionOfEntryId.HasValue
                    ? CoinTransactionKind.Correction
                    : x.RewardType.HasValue
                        ? CoinTransactionKind.Reward
                        : x.DeltaCoins < 0
                            ? CoinTransactionKind.Debit
                            : CoinTransactionKind.Correction,
                x.DeltaCoins,
                x.RewardType,
                x.SourceRecordId,
                "Posted",
                x.RecordedAtUtc,
                x.CorrectionOfEntryId))
            .ToListAsync(cancellationToken);

        var reservations = await _db.CoinReservations
            .AsNoTracking()
            .Where(x => x.WalletId == walletId.Value)
            .Select(x => new
            {
                x.Id,
                x.RequestId,
                x.ReservedCoins,
                x.Status,
                x.CreatedAtUtc,
                x.ResolvedAtUtc
            })
            .ToListAsync(cancellationToken);

        var history = new List<CoinTransactionResponse>(
            ledger.Count + reservations.Count * 2);

        history.AddRange(ledger);

        if (!query.RewardType.HasValue)
        {
            foreach (var reservation in reservations)
            {
                history.Add(new CoinTransactionResponse(
                    reservation.Id,
                    CoinTransactionKind.Hold,
                    -reservation.ReservedCoins,
                    null,
                    reservation.RequestId.ToString("D"),
                    reservation.Status switch
                    {
                        CoinReservationState.Pending => "Pending",
                        CoinReservationState.Consumed => "Consumed",
                        CoinReservationState.Released => "Released",
                        _ => "Unknown"
                    },
                    reservation.CreatedAtUtc,
                    null));

                if (reservation.Status ==
                        CoinReservationState.Released &&
                    reservation.ResolvedAtUtc.HasValue)
                {
                    history.Add(new CoinTransactionResponse(
                        reservation.Id,
                        CoinTransactionKind.Release,
                        reservation.ReservedCoins,
                        null,
                        reservation.RequestId.ToString("D"),
                        "Released",
                        reservation.ResolvedAtUtc.Value,
                        null));
                }
            }
        }

        IEnumerable<CoinTransactionResponse> filtered = history;

        if (query.Kind.HasValue)
        {
            filtered = filtered.Where(
                x => x.Kind == query.Kind.Value);
        }

        var ordered = filtered
            .OrderByDescending(x => x.RecordedAtUtc)
            .ThenByDescending(x => x.TransactionId)
            .ToList();

        int totalCount = ordered.Count;
        int skip = checked((query.Page - 1) * query.PageSize);

        return new CoinTransactionHistoryResponse(
            query.Page,
            query.PageSize,
            totalCount,
            ordered.Skip(skip).Take(query.PageSize).ToArray());
    }
}
