
using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

using Wayhunter.Application.Modules.CoinsWallet.Dtos;
using Wayhunter.Application.Modules.CoinsWallet.Interfaces;
using Wayhunter.Application.Modules.CoinsWallet.Validators;

using Wayhunter.Domain.Modules.CoinsWallet.Enums;

using Wayhunter.Infrastructure.Persistence.DbContexts;
using Wayhunter.Infrastructure.Persistence.Models.CoinsWallet;

namespace Wayhunter.Infrastructure.Persistence.Repositories.CoinsWallet;

public sealed class SqlCoinRewardOutboxStore
    : ICoinRewardOutboxStore
{
    private readonly WayhunterSharedDbContext _db;

    public SqlCoinRewardOutboxStore(
        WayhunterSharedDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    // ==========================================
    // Enqueue a trusted reward
    // ==========================================

    public async Task<bool> EnqueueIfMissingAsync(
        CoinRewardGrantRequest reward,
        DateTimeOffset queuedAtUtc,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        CoinRewardContractRules.ValidateGrant(reward);
        RequireUtc(queuedAtUtc, nameof(queuedAtUtc));

        string sourceId = reward.SourceRecordId.Trim();
        string ownerId = reward.RecipientUserId.Trim();
        string ruleVersion = reward.RuleVersion.Trim();

        if (sourceId.Length > 256 ||
            ownerId.Length > 256 ||
            ruleVersion.Length > 64 ||
            reward.ApprovalRecordId?.Length > 256 ||
            reward.GameAttemptId?.Length > 256)
        {
            throw new ArgumentException(
                "Reward values exceed SQL column limits.");
        }

        var row = new CoinRewardOutboxRow
        {
            EventId = reward.EventId,
            RewardType = reward.RewardType,
            SourceRecordId = sourceId,
            RecipientUserId = ownerId,
            CoinsAmount = reward.CoinsAmount,
            EligibilityConfirmedAtUtc =
                reward.EligibilityConfirmedAtUtc,
            CorrelationId = reward.CorrelationId,
            RuleVersion = ruleVersion,
            ApprovalRecordId = reward.ApprovalRecordId,
            GameAttemptId = reward.GameAttemptId,

            Status = CoinRewardOutboxState.Pending,
            AttemptNumber = 0,
            NextDueAtUtc = queuedAtUtc,
            LeaseUntilUtc = null,
            ClaimToken = null,
            LastErrorCode = null,
            DeliveredAtUtc = null
        };

        try
        {
            _db.CoinRewardOutbox.Add(row);

            await _db.SaveChangesAsync(
                cancellationToken);

            return true;
        }
        catch (DbUpdateException ex)
            when (IsUniqueViolation(ex))
        {
            // Remove the failed insert from EF tracking.
            _db.Entry(row).State = EntityState.Detached;

            // A uniqueness violation could be unrelated.
            // Confirm the actual reward before treating
            // the operation as an idempotent replay.
            bool duplicate = await _db.CoinRewardOutbox
                .AsNoTracking()
                .AnyAsync(x =>
                    x.EventId == reward.EventId ||
                    (
                        x.RewardType == reward.RewardType &&
                        x.SourceRecordId == sourceId &&
                        (
                            x.RecipientUserId == ownerId ||
                            reward.RewardType ==
                                CoinRewardType.GameWinnerApproved
                        )
                    ),
                    cancellationToken);

            if (duplicate)
                return false;

            throw;
        }
    }

    // ==========================================
    // Atomically claim one due reward
    // ==========================================

    public async Task<ClaimedCoinRewardOutboxItem?>
        TryClaimNextDueAsync(
            DateTimeOffset nowUtc,
            TimeSpan leaseDuration,
            CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        RequireUtc(nowUtc, nameof(nowUtc));

        if (leaseDuration <= TimeSpan.Zero ||
            leaseDuration > TimeSpan.FromHours(1))
        {
            throw new ArgumentOutOfRangeException(
                nameof(leaseDuration));
        }

        DateTimeOffset leaseUntilUtc =
            nowUtc.Add(leaseDuration);

        string claimToken =
            Guid.NewGuid().ToString("N");

        // One SQL UPDATE statement performs selection
        // and state transition atomically.
        //
        // UPDLOCK prevents competing workers from
        // claiming the same row simultaneously.
        //
        // READPAST allows workers to skip rows
        // locked by another worker.
        //
        // Expired Processing leases are claimable again.

        const string sql = """
            ;WITH due AS
            (
                SELECT TOP (1) *
                FROM [CoinRewardOutbox]
                    WITH (UPDLOCK, READPAST, ROWLOCK)
                WHERE
                    (
                        [Status] = 1
                        AND [NextDueAtUtc] <= @now
                    )
                    OR
                    (
                        [Status] = 2
                        AND [LeaseUntilUtc] <= @now
                    )
                ORDER BY
                    [NextDueAtUtc],
                    [EventId]
            )
            UPDATE due
            SET
                [Status] = 2,
                [ClaimToken] = @claimToken,
                [LeaseUntilUtc] = @leaseUntil,
                [AttemptNumber] = [AttemptNumber] + 1
            OUTPUT
                INSERTED.[EventId],
                INSERTED.[ClaimToken],
                INSERTED.[AttemptNumber],
                INSERTED.[RewardType],
                INSERTED.[SourceRecordId],
                INSERTED.[RecipientUserId],
                INSERTED.[CoinsAmount],
                INSERTED.[EligibilityConfirmedAtUtc],
                INSERTED.[CorrelationId],
                INSERTED.[RuleVersion],
                INSERTED.[ApprovalRecordId],
                INSERTED.[GameAttemptId];
            """;

        var connection =
            (SqlConnection)_db.Database.GetDbConnection();

        bool openedHere =
            connection.State != ConnectionState.Open;

        if (openedHere)
        {
            await _db.Database.OpenConnectionAsync(
                cancellationToken);
        }

        try
        {
            await using var command =
                new SqlCommand(sql, connection);

            if (_db.Database.CurrentTransaction is
                IDbContextTransaction transaction)
            {
                command.Transaction =
                    (SqlTransaction)transaction
                        .GetDbTransaction();
            }

            command.Parameters.Add(
                new SqlParameter(
                    "@now", SqlDbType.DateTimeOffset)
                {
                    Value = nowUtc
                });

            command.Parameters.Add(
                new SqlParameter(
                    "@leaseUntil", SqlDbType.DateTimeOffset)
                {
                    Value = leaseUntilUtc
                });

            command.Parameters.Add(
                new SqlParameter(
                    "@claimToken", SqlDbType.NVarChar, 128)
                {
                    Value = claimToken
                });

            await using var reader =
                await command.ExecuteReaderAsync(
                    cancellationToken);

            if (!await reader.ReadAsync(
                cancellationToken))
            {
                return null;
            }

            var reward = new CoinRewardGrantRequest(
                EventId: reader.GetGuid(0),
                RewardType:
                    (CoinRewardType)reader.GetInt32(3),
                SourceRecordId: reader.GetString(4),
                RecipientUserId: reader.GetString(5),
                CoinsAmount: reader.GetInt32(6),
                EligibilityConfirmedAtUtc:
                    reader.GetDateTimeOffset(7),
                CorrelationId: reader.GetGuid(8),
                RuleVersion: reader.GetString(9),
                ApprovalRecordId:
                    reader.IsDBNull(10)
                        ? null
                        : reader.GetString(10),
                GameAttemptId:
                    reader.IsDBNull(11)
                        ? null
                        : reader.GetString(11)
            );

            return new ClaimedCoinRewardOutboxItem(
                OutboxId: reader.GetGuid(0),
                ClaimToken: reader.GetString(1),
                Reward: reward,
                AttemptNumber: reader.GetInt32(2));
        }
        finally
        {
            if (openedHere)
            {
                await _db.Database.CloseConnectionAsync();
            }
        }
    }

    // ==========================================
    // Confirm successful reward delivery
    // ==========================================

    public async Task MarkDeliveredAsync(
        Guid outboxId,
        string claimToken,
        DateTimeOffset deliveredAtUtc,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        RequireClaim(outboxId, claimToken);
        RequireUtc(deliveredAtUtc, nameof(deliveredAtUtc));

        int updated = await _db.CoinRewardOutbox
            .Where(x =>
                x.EventId == outboxId &&
                x.Status == CoinRewardOutboxState.Processing &&
                x.ClaimToken == claimToken)
            .ExecuteUpdateAsync(
                updates => updates
                    .SetProperty(
                        x => x.Status,
                        CoinRewardOutboxState.Delivered)
                    .SetProperty(
                        x => x.DeliveredAtUtc,
                        deliveredAtUtc)
                    .SetProperty(
                        x => x.LeaseUntilUtc,
                        (DateTimeOffset?)null)
                    .SetProperty(
                        x => x.ClaimToken,
                        (string?)null)
                    .SetProperty(
                        x => x.LastErrorCode,
                        (string?)null),
                cancellationToken);

        if (updated != 1)
        {
            throw new InvalidOperationException(
                "Reward claim is no longer owned by this worker.");
        }
    }

    // ==========================================
    // Schedule failed reward for retry
    // ==========================================

    public async Task ScheduleRetryAsync(
        Guid outboxId,
        string claimToken,
        DateTimeOffset nextDueAtUtc,
        string errorCode,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        RequireClaim(outboxId, claimToken);
        RequireUtc(nextDueAtUtc, nameof(nextDueAtUtc));

        if (string.IsNullOrWhiteSpace(errorCode))
        {
            throw new ArgumentException(
                "A retry error code is required.",
                nameof(errorCode));
        }

        string normalizedError = errorCode.Trim();

        if (normalizedError.Length > 128)
        {
            throw new ArgumentException(
                "Retry error code exceeds SQL column limit.",
                nameof(errorCode));
        }

        int updated = await _db.CoinRewardOutbox
            .Where(x =>
                x.EventId == outboxId &&
                x.Status == CoinRewardOutboxState.Processing &&
                x.ClaimToken == claimToken)
            .ExecuteUpdateAsync(
                updates => updates
                    .SetProperty(
                        x => x.Status,
                        CoinRewardOutboxState.Pending)
                    .SetProperty(
                        x => x.NextDueAtUtc,
                        nextDueAtUtc)
                    .SetProperty(
                        x => x.LeaseUntilUtc,
                        (DateTimeOffset?)null)
                    .SetProperty(
                        x => x.ClaimToken,
                        (string?)null)
                    .SetProperty(
                        x => x.LastErrorCode,
                        normalizedError),
                cancellationToken);

        if (updated != 1)
        {
            throw new InvalidOperationException(
                "Reward claim is no longer owned by this worker.");
        }
    }

    // ==========================================
    // Validation Helpers
    // ==========================================

    private static void RequireUtc(
        DateTimeOffset value,
        string parameterName)
    {
        if (value == default ||
            value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "A valid UTC timestamp is required.",
                parameterName);
        }
    }

    private static void RequireClaim(
        Guid outboxId,
        string claimToken)
    {
        if (outboxId == Guid.Empty)
        {
            throw new ArgumentException(
                "Outbox ID is required.",
                nameof(outboxId));
        }

        if (string.IsNullOrWhiteSpace(claimToken) ||
            claimToken.Length > 128)
        {
            throw new ArgumentException(
                "Valid claim token is required.",
                nameof(claimToken));
        }
    }

    private static bool IsUniqueViolation(
        DbUpdateException exception)
    {
        return exception.InnerException is SqlException sql &&
               (sql.Number == 2601 || sql.Number == 2627);
    }
}
