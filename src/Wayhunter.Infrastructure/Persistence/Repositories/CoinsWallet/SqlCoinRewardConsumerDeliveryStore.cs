using System.Data;
using Microsoft.EntityFrameworkCore;
using Wayhunter.Application.Modules.CoinsWallet.Interfaces;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Wayhunter.Infrastructure.Persistence.Models.CoinsWallet;

namespace Wayhunter.Infrastructure.Persistence.Repositories.CoinsWallet;

public sealed class SqlCoinRewardConsumerDeliveryStore
    : ICoinRewardConsumerDeliveryStore
{
    private readonly WayhunterSharedDbContext _db;
    public SqlCoinRewardConsumerDeliveryStore(WayhunterSharedDbContext db) =>
        _db = db ?? throw new ArgumentNullException(nameof(db));

    public Task<bool> IsDeliveredAsync(
        Guid eventId, string consumerName,
        CancellationToken cancellationToken = default) =>
        _db.CoinRewardConsumerDeliveries.AsNoTracking().AnyAsync(
            x => x.EventId == eventId &&
                 x.ConsumerName == Normalize(consumerName) &&
                 x.Status == CoinRewardConsumerDeliveryState.Delivered,
            cancellationToken);

    public Task RecordOutcomeAsync(
        Guid eventId, string consumerName, bool delivered,
        DateTimeOffset attemptedAtUtc, string? errorCode,
        CancellationToken cancellationToken = default)
    {
        if (eventId == Guid.Empty || attemptedAtUtc == default ||
            attemptedAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Invalid consumer delivery outcome.");
        string name = Normalize(consumerName);
        string? error = errorCode?.Trim();
        if (error?.Length > 128)
            throw new ArgumentException("Consumer error code is too long.");

        return _db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            _db.ChangeTracker.Clear();
            await using var tx = await _db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);
            await SqlServerTransactionLock.AcquireAsync(
                _db, "reward-consumer", $"{eventId:D}:{name}", cancellationToken);
            var row = await _db.CoinRewardConsumerDeliveries.SingleOrDefaultAsync(
                x => x.EventId == eventId && x.ConsumerName == name,
                cancellationToken);
            if (row?.Status == CoinRewardConsumerDeliveryState.Delivered)
            {
                await tx.RollbackAsync(cancellationToken);
                return;
            }
            row ??= new CoinRewardConsumerDeliveryRow
            {
                Id = Guid.NewGuid(),
                EventId = eventId,
                ConsumerName = name,
                Status = CoinRewardConsumerDeliveryState.Pending
            };
            if (_db.Entry(row).State == EntityState.Detached)
                _db.CoinRewardConsumerDeliveries.Add(row);
            row.AttemptCount = checked(row.AttemptCount + 1);
            row.Status = delivered
                ? CoinRewardConsumerDeliveryState.Delivered
                : CoinRewardConsumerDeliveryState.RetryPending;
            row.DeliveredAtUtc = delivered ? attemptedAtUtc : null;
            row.LastErrorCode = delivered ? null : error ?? "UNKNOWN";
            await _db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
        });
    }

    private static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > 100)
            throw new ArgumentException("Consumer name is required.");
        return value.Trim();
    }
}
