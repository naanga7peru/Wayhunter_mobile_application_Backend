
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
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

        string userId = memberUserId.Trim();

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

        // Serializable isolation protects the
        // membership transition and outbox insertion
        // within a single transaction.

        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        var member = await _dbContext.TripMembers
            .SingleOrDefaultAsync(
                x =>
                    x.Id == memberId &&
                    x.TripPlanId == tripPlanId,
                cancellationToken);

        if (member is null)
        {
            throw new KeyNotFoundException(
                "Trip member was not found.");
        }

        // Preserve historical inactive membership.
        // Repeated removal must be idempotent.

        if (!member.IsActive)
        {
            await transaction.RollbackAsync(
                cancellationToken);

            return false;
        }

        if (removedAtUtc < member.JoinedAtUtc)
        {
            throw new InvalidOperationException(
                "Removal cannot precede joining.");
        }

        // One revocation event per membership.

        bool notificationExists =
            await _dbContext.TripMembershipRevocationOutbox
                .AnyAsync(
                    x => x.MemberId == memberId,
                    cancellationToken);

        if (notificationExists)
        {
            throw new InvalidOperationException(
                "A revocation notification already exists.");
        }

        // Apply the domain membership transition.
        // This never deletes historical membership data.

        bool removed = member.Remove(removedAtUtc);

        if (!removed)
        {
            await transaction.RollbackAsync(
                cancellationToken);

            return false;
        }

        // Queue notification for later delivery.
        // No notification is sent directly here.
        // GPS consent is not automatically changed.

        var notification =
            new TripMembershipRevocationOutboxRow
            {
                Id = Guid.NewGuid(),
                TripPlanId = tripPlanId,
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

        // Membership update and outbox insertion
        // must be committed together.

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        await transaction.CommitAsync(
            cancellationToken);

        return true;
    }
}
