
using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore.Storage;
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

        var strategy = _db.Database.CreateExecutionStrategy();

        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                // A retry can reuse this scoped DbContext after an
                // uncertain commit. Never retain entities from the
                // previous attempt in the change tracker.
                _db.ChangeTracker.Clear();

                await using var transaction =
                    await _db.Database.BeginTransactionAsync(
                        IsolationLevel.Serializable,
                        cancellationToken);

                // Acquire every application lock before the reads it
                // protects. The global winner lock must precede the
                // wallet lock so all callers use one lock order.
                if (request.RewardType ==
                    CoinRewardType.GameWinnerApproved)
                {
                    await AcquireTransactionLockAsync(
                        "game-winner",
                        sourceId,
                        cancellationToken);
                }

                await AcquireTransactionLockAsync(
                    "wallet",
                    ownerId,
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
                    {
                        await transaction.RollbackAsync(
                            cancellationToken);
                        return false;
                    }
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
                    {
                        await transaction.RollbackAsync(
                            cancellationToken);
                        return false;
                    }
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

                // The immutable ledger append and wallet projection are
                // committed by the same retryable transaction.
                await _db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return true;
            });
        }
        catch (DbUpdateException ex) when (
            IsUniqueConstraintViolation(ex))
        {
            // Database uniqueness constraints protect against
            // concurrent duplicate reward commits. Confirm the
            // conflicting committed reward before returning false.
            _db.ChangeTracker.Clear();

            bool duplicate = await strategy.ExecuteAsync(() =>
                _db.CoinLedgerEntries
                    .AsNoTracking()
                    .AnyAsync(
                        x => x.RewardType == request.RewardType &&
                             x.SourceRecordId == sourceId &&
                             (request.RewardType ==
                                 CoinRewardType.GameWinnerApproved ||
                              _db.CoinWallets.Any(w =>
                                  w.Id == x.WalletId &&
                                  w.UserId == ownerId)),
                        cancellationToken));

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



    private async Task AcquireTransactionLockAsync(
        string category,
        string key,
        CancellationToken cancellationToken)
    {
        var hash = Convert.ToHexString(
            SHA256.HashData(
                Encoding.UTF8.GetBytes(key)));

        string resource = $"m6:{category}:{hash}";

        var currentTransaction =
            _db.Database.CurrentTransaction
            ?? throw new InvalidOperationException(
                "A SQL transaction is required.");

        var connection = _db.Database.GetDbConnection();

        await using var command = connection.CreateCommand();

        command.Transaction =
            currentTransaction.GetDbTransaction();

        command.CommandText = """
        DECLARE @lockResult int;

        EXEC @lockResult = sys.sp_getapplock
            @Resource = @resource,
            @LockMode = 'Exclusive',
            @LockOwner = 'Transaction',
            @LockTimeout = 15000;

        SELECT @lockResult;
        """;

        var parameter = command.CreateParameter();
        parameter.ParameterName = "@resource";
        parameter.Value = resource;

        command.Parameters.Add(parameter);

        object? result =
            await command.ExecuteScalarAsync(
                cancellationToken);

        int lockResult = Convert.ToInt32(result);

        if (lockResult < 0)
        {
            throw new InvalidOperationException(
                $"Could not acquire M6 transaction lock: {lockResult}");
        }
    }



}
