
using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Wayhunter.Application.Modules.CoinsWallet.Dtos;
using Wayhunter.Application.Modules.CoinsWallet.Interfaces;
using Wayhunter.Application.Modules.CoinsWallet.Validators;
using Wayhunter.Domain.Modules.CoinsWallet.Enums;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Wayhunter.Infrastructure.Persistence.Models.CoinsWallet;

namespace Wayhunter.Infrastructure.Persistence.Repositories.CoinsWallet;

public sealed class SqlCoinRewardGrantStore : ICoinRewardGrantStore
{
    private readonly WayhunterSharedDbContext _db;

    public SqlCoinRewardGrantStore(WayhunterSharedDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<bool> TryGrantOnceAsync(
        CoinRewardGrantRequest request,
        CancellationToken cancellationToken = default)
    {
        CoinRewardContractRules.ValidateGrant(request);

        string ownerId = request.RecipientUserId.Trim();
        string sourceId = request.SourceRecordId.Trim();
        string ruleVersion = request.RuleVersion.Trim();

        if (ownerId.Length > 256 ||
            sourceId.Length > 256 ||
            ruleVersion.Length > 64)
        {
            throw new ArgumentException(
                "Reward values exceed SQL column limits.");
        }

        try
        {
            await using var transaction =
                await _db.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable,
                    cancellationToken);

            var wallet = await _db.CoinWallets
                .SingleOrDefaultAsync(
                    x => x.UserId == ownerId,
                    cancellationToken);

            // One verified game winner per source globally.
            if (request.RewardType ==
                CoinRewardType.GameWinnerApproved)
            {
                bool winnerExists = await _db.CoinLedgerEntries
                    .AnyAsync(
                        x => x.RewardType == request.RewardType &&
                             x.SourceRecordId == sourceId,
                        cancellationToken);

                if (winnerExists)
                    return false;
            }

            if (wallet is not null)
            {
                bool alreadyGranted = await _db.CoinLedgerEntries
                    .AnyAsync(
                        x => x.WalletId == wallet.Id &&
                             x.RewardType == request.RewardType &&
                             x.SourceRecordId == sourceId,
                        cancellationToken);

                if (alreadyGranted)
                    return false;
            }
            else
            {
                wallet = new CoinWalletRow
                {
                    Id = Guid.NewGuid(),
                    UserId = ownerId,
                    PostedCoins = 0,
                    ReservedCoins = 0,
                    LifetimeEarnedCoins = 0
                };

                _db.CoinWallets.Add(wallet);
            }

            wallet.PostedCoins = checked(
                wallet.PostedCoins + request.CoinsAmount);

            wallet.LifetimeEarnedCoins = checked(
                wallet.LifetimeEarnedCoins + request.CoinsAmount);

            _db.CoinLedgerEntries.Add(new CoinLedgerEntryRow
            {
                Id = Guid.NewGuid(),
                WalletId = wallet.Id,
                DeltaCoins = request.CoinsAmount,
                RewardType = request.RewardType,
                SourceRecordId = sourceId,
                RuleVersion = ruleVersion,
                CorrelationId = request.CorrelationId,
                RecordedAtUtc = request.EligibilityConfirmedAtUtc
            });

            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return true;
        }
        catch (DbUpdateException ex) when (
            IsUniqueConstraintViolation(ex))
        {
            // Database uniqueness constraints protect against
            // concurrent duplicate reward commits. Confirm the
            // conflicting committed reward before returning false.
            bool duplicate = await _db.CoinLedgerEntries
                .AsNoTracking()
                .AnyAsync(
                    x => x.RewardType == request.RewardType &&
                         x.SourceRecordId == sourceId &&
                         (request.RewardType ==
                             CoinRewardType.GameWinnerApproved ||
                          _db.CoinWallets.Any(w =>
                              w.Id == x.WalletId &&
                              w.UserId == ownerId)),
                    cancellationToken);

            if (duplicate)
                return false;

            // Could be an unrelated unique constraint violation.
            throw;
        }
    }

    private static bool IsUniqueConstraintViolation(
        DbUpdateException exception)
    {
        return exception.InnerException is SqlException sql &&
               (sql.Number == 2601 || sql.Number == 2627);
    }
}
