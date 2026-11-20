using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Wayhunter.Application.Modules.TripSafety.Interfaces;
using Wayhunter.Domain.Modules.IdentitySocial.Enums;
using Wayhunter.Domain.Modules.TravelPlanning.Enums;
using Wayhunter.Domain.Modules.TripSafety.Enums;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Wayhunter.Infrastructure.Persistence.Models.TripSafety;

namespace Wayhunter.Infrastructure.Persistence.Repositories.TripSafety;

public sealed class EfTripSafeCheckInStore(IDbContextFactory<WayhunterSharedDbContext> contexts)
    : ITripSafetyCheckInStore
{
    public async Task<TripSafeCheckInWriteResult> RecordAsync(
        TripSafeCheckIn checkIn, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(checkIn);
        cancellationToken.ThrowIfCancellationRequested();
        if (!Guid.TryParseExact(checkIn.UserId, "D", out var actor) || actor == Guid.Empty)
            throw new UnauthorizedAccessException("Trusted user identity is required.");

        if (checkIn.CheckInId == Guid.Empty || checkIn.TripPlanId == Guid.Empty ||
            checkIn.SessionId == Guid.Empty || checkIn.CheckedInAtUtc == default ||
            checkIn.CheckedInAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Valid check-in IDs and UTC timestamp are required.", nameof(checkIn));

        var normalized = checkIn with { UserId = actor.ToString("D") };
        var eventId = Guid.NewGuid();
        try
        {
            await using var strategyContext = await contexts.CreateDbContextAsync(cancellationToken);
            if (!strategyContext.Database.IsSqlServer())
                throw new TripSafetyCheckInUnavailableException("SQL Server safe check-in persistence is required.");

            var strategy = strategyContext.Database.CreateExecutionStrategy();
            try
            {
                return await strategy.ExecuteAsync(
                    ct => WriteAsync(normalized, eventId, ct), cancellationToken);
            }
            catch (DbUpdateException error) when (error.InnerException is SqlException { Number: 2601 or 2627 })
            {
                // A constraint error alone establishes nothing about the logical check-in.
                // Re-enter authorization and confirm the exact committed record and event.
                return await strategy.ExecuteAsync(
                    ct => WriteAsync(normalized, eventId, ct), cancellationToken);
            }
        }
        catch (Exception error) when (error is DbException or DbUpdateException or
            Microsoft.EntityFrameworkCore.Storage.RetryLimitExceededException or TimeoutException)
        {
            throw new TripSafetyCheckInUnavailableException("Durable safe check-in persistence is unavailable.");
        }
    }

    private async Task<TripSafeCheckInWriteResult> WriteAsync(
        TripSafeCheckIn command, Guid eventId, CancellationToken cancellationToken)
    {
        // Each execution-strategy attempt uses a clean context and SQL transaction.
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);

        var accountId = await db.Users.AsNoTracking()
            .Where(x => x.UserId == command.UserId && x.Status == UserAccountStatus.Active &&
                x.VerifiedAtUtc != null && x.DeletionRequestedAtUtc == null)
            .Select(x => x.UserId).SingleOrDefaultAsync(cancellationToken);
        if (!string.Equals(accountId, command.UserId, StringComparison.Ordinal))
            throw new UnauthorizedAccessException("An active verified account is required.");

        var trip = await db.Trips.AsNoTracking().Where(x => x.Id == command.TripPlanId)
            .Select(x => new { x.OwnerUserId, x.DraftStatus }).SingleOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("Trip plan was not found.");

        var session = await db.LiveTripSessions.AsNoTracking()
            .Where(x => x.SessionId == command.SessionId && x.TripPlanId == command.TripPlanId)
            .Select(x => (LiveTripStatus?)x.Status).SingleOrDefaultAsync(cancellationToken);

        if (!string.Equals(trip.OwnerUserId, command.UserId, StringComparison.Ordinal))
        {
            var memberId = await db.TripMembers.AsNoTracking()
                .Where(x => x.TripPlanId == command.TripPlanId && x.MemberUserId == command.UserId && x.IsActive)
                .Select(x => x.MemberUserId).SingleOrDefaultAsync(cancellationToken);
            if (!string.Equals(memberId, command.UserId, StringComparison.Ordinal))
                throw new UnauthorizedAccessException("Only the owner or an active member may check in.");
        }

        if (trip.DraftStatus == TripDraftStatus.Cancelled)
            throw new InvalidOperationException("Cancelled trips cannot receive check-ins.");
        if (session is null)
            throw new KeyNotFoundException("Trip session was not found.");

        var existing = await db.TripSafeCheckIns.AsNoTracking()
            .SingleOrDefaultAsync(x => x.SessionId == command.SessionId && x.UserId == command.UserId, cancellationToken);
        if (existing is not null)
        {
            if (existing.TripPlanId != command.TripPlanId ||
                !string.Equals(existing.UserId, command.UserId, StringComparison.Ordinal) ||
                !await db.TripSafeCheckInOutbox.AnyAsync(x => x.CheckInId == existing.CheckInId, cancellationToken))
                throw new TripSafetyCheckInUnavailableException("Committed check-in could not be verified.");

            await transaction.CommitAsync(cancellationToken);
            return new TripSafeCheckInWriteResult(
                new TripSafeCheckIn(existing.CheckInId, existing.TripPlanId, existing.SessionId,
                    existing.UserId, existing.CheckedInAtUtc), false);
        }

        if (session is not (LiveTripStatus.Active or LiveTripStatus.Paused))
            throw new InvalidOperationException("Only open trip sessions can receive check-ins.");

        db.TripSafeCheckIns.Add(new TripSafeCheckInRow
        {
            CheckInId = command.CheckInId,
            TripPlanId = command.TripPlanId,
            SessionId = command.SessionId,
            UserId = command.UserId,
            CheckedInAtUtc = command.CheckedInAtUtc
        });
        db.TripSafeCheckInOutbox.Add(new TripSafeCheckInOutboxRow
        {
            Id = eventId,
            CheckInId = command.CheckInId,
            RecipientUserId = trip.OwnerUserId,
            CreatedAtUtc = command.CheckedInAtUtc
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new TripSafeCheckInWriteResult(command, true);
    }
}
