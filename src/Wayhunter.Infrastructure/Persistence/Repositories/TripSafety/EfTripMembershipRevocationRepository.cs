
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

using Wayhunter.Application.Modules.TripSafety.Interfaces;
using Wayhunter.Domain.Modules.TravelPlanning.Entities;
using Wayhunter.Domain.Modules.TripSafety.Entities;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Wayhunter.Infrastructure.Persistence.Models.TripSafety;

namespace Wayhunter.Infrastructure.Persistence.Repositories.TripSafety;

public sealed class EfTripMembershipRevocationRepository
    : ITripMembershipRevocationRepository
{
    private readonly WayhunterSharedDbContext _dbContext;

    public EfTripMembershipRevocationRepository(
        WayhunterSharedDbContext dbContext)
    {
        _dbContext = dbContext
            ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<TripPlan?> GetTripPlanByIdAsync(
        Guid tripPlanId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (tripPlanId == Guid.Empty)
        {
            throw new ArgumentException(
                "Valid Trip Plan ID is required.",
                nameof(tripPlanId));
        }

        return await _dbContext.Trips
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Id == tripPlanId,
                cancellationToken);
    }

    public async Task<TripMember?> GetMemberAsync(
        Guid tripPlanId,
        string memberUserId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (tripPlanId == Guid.Empty)
        {
            throw new ArgumentException(
                "Valid Trip Plan ID is required.",
                nameof(tripPlanId));
        }

        if (string.IsNullOrWhiteSpace(memberUserId))
        {
            throw new ArgumentException(
                "Member User ID is required.",
                nameof(memberUserId));
        }

        var userId = memberUserId.Trim();

        return await _dbContext.TripMembers
            .AsNoTracking()
            .Where(x =>
                x.TripPlanId == tripPlanId &&
                x.MemberUserId == userId)
            .OrderByDescending(x => x.IsActive)
            .ThenByDescending(x => x.JoinedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> RemoveMemberAndQueueNotificationAsync(
        Guid tripPlanId,
        Guid memberId,
        DateTimeOffset removedAtUtc,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (tripPlanId == Guid.Empty)
        {
            throw new ArgumentException(
                "Valid Trip Plan ID is required.",
                nameof(tripPlanId));
        }

        if (memberId == Guid.Empty)
        {
            throw new ArgumentException(
                "Valid Member ID is required.",
                nameof(memberId));
        }

        if (removedAtUtc == default ||
            removedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "Valid UTC removal timestamp is required.",
                nameof(removedAtUtc));
        }

        // Keep the membership transition and outbox
        // insertion in the same database transaction.

        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        // SQL Server only:
        // Serialize removal operations targeting
        // the same membership before reading its state.
        //
        // The lock is owned by this transaction and
        // is released on commit or rollback.

        if (_dbContext.Database.IsSqlServer())
        {
            await AcquireMembershipLockAsync(
                memberId,
                cancellationToken);
        }

        TripMember? member;

        if (_dbContext.Database.IsSqlServer())
        {
            member = await _dbContext.TripMembers
                .FromSqlInterpolated(
                    $"""
                    SELECT *
                    FROM [TripSafety_TripMembers]
                        WITH (UPDLOCK, HOLDLOCK)
                    WHERE [Id] = {memberId}
                      AND [TripPlanId] = {tripPlanId}
                    """)
                .SingleOrDefaultAsync(cancellationToken);
        }
        else
        {
            // Existing EF InMemory tests use this path.
            // It does not provide SQL concurrency guarantees.

            member = await _dbContext.TripMembers
                .SingleOrDefaultAsync(
                    x =>
                        x.Id == memberId &&
                        x.TripPlanId == tripPlanId,
                    cancellationToken);
        }

        if (member is null)
        {
            throw new KeyNotFoundException(
                "Trip member was not found.");
        }

        // The second concurrent request should see
        // the state committed by the first request.

        if (!member.IsActive)
        {
            return false;
        }

        if (removedAtUtc < member.JoinedAtUtc)
        {
            throw new InvalidOperationException(
                "Removal cannot precede joining.");
        }

        // Prevent duplicate revocation events.
        // The unique MemberId index also protects
        // against duplicate database inserts.

        var notificationExists =
            await _dbContext.TripMembershipRevocationOutbox
                .AnyAsync(
                    x => x.MemberId == member.Id,
                    cancellationToken);

        if (notificationExists)
        {
            throw new InvalidOperationException(
                "An active membership already has " +
                "a revocation notification.");
        }

        // Preserve the membership history.
        // Do not delete the membership row.

        var removed = member.Remove(removedAtUtc);

        if (!removed)
        {
            return false;
        }

        // Add one pending notification.
        // Delivery happens separately.
        // GPS consent is not automatically restored.

        var notification =
            new TripMembershipRevocationOutboxRow
            {
                Id = Guid.NewGuid(),
                TripPlanId = member.TripPlanId,
                MemberId = member.Id,
                MemberUserId = member.MemberUserId,
                RemovedAtUtc = removedAtUtc,
                CreatedAtUtc = removedAtUtc,
                IsDelivered = false,
                DeliveredAtUtc = null,
                DeliveryAttempts = 0
            };

        _dbContext.TripMembershipRevocationOutbox
            .Add(notification);

        // Both changes must commit together.

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        await transaction.CommitAsync(
            cancellationToken);

        return true;
    }

    private async Task AcquireMembershipLockAsync(
        Guid memberId,
        CancellationToken cancellationToken)
    {
        // Must run inside the transaction created
        // by RemoveMemberAndQueueNotificationAsync.

        var currentTransaction =
            _dbContext.Database.CurrentTransaction
            ?? throw new InvalidOperationException(
                "A SQL transaction is required.");

        var connection =
            _dbContext.Database.GetDbConnection();

        await using var command =
            connection.CreateCommand();

        command.Transaction =
            currentTransaction.GetDbTransaction();

        command.CommandType = CommandType.Text;

        command.CommandText =
            """
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
        parameter.DbType = DbType.String;
        parameter.Size = 255;

        parameter.Value =
            $"M3:MembershipRevocation:{memberId:N}";

        command.Parameters.Add(parameter);

        var result =
            await command.ExecuteScalarAsync(
                cancellationToken);

        if (result is null ||
            result is DBNull)
        {
            throw new InvalidOperationException(
                "SQL Server did not return an application lock result.");
        }

        var lockResult = Convert.ToInt32(result);

        // SQL Server sp_getapplock:
        // >= 0 means the lock was granted.
        // < 0 means timeout, cancellation,
        // deadlock victim, or another lock error.

        if (lockResult < 0)
        {
            throw new InvalidOperationException(
                $"Could not acquire M3 membership lock. " +
                $"SQL result: {lockResult}.");
        }
    }
}
