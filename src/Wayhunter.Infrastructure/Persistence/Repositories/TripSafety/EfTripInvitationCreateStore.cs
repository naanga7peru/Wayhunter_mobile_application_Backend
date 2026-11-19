
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;

using Wayhunter.Application.Modules.TripSafety.Interfaces;

using Wayhunter.Domain.Modules.IdentitySocial.Enums;
using Wayhunter.Domain.Modules.TravelPlanning.Enums;
using Wayhunter.Domain.Modules.TripSafety.Entities;
using Wayhunter.Domain.Modules.TripSafety.Enums;

using Wayhunter.Infrastructure.Persistence.DbContexts;

namespace Wayhunter.Infrastructure.Persistence.Repositories.TripSafety;

public sealed class EfTripInvitationCreateStore
    : ITripInvitationCreateStore
{
    private readonly WayhunterSharedDbContext _db;

    public EfTripInvitationCreateStore(
        WayhunterSharedDbContext db)
    {
        _db = db
            ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task CreateAsync(
        TripInvitation invitation,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ArgumentNullException.ThrowIfNull(invitation);

        if (invitation.Status != TripInvitationStatus.Pending)
        {
            throw new InvalidOperationException(
                "Only pending invitations can be created.");
        }

        var strategy =
            _db.Database.CreateExecutionStrategy();

        var attempt = 0;

        await strategy.ExecuteAsync(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (attempt++ > 0)
            {
                _db.ChangeTracker.Clear();
            }

            await using var transaction =
                await _db.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable,
                    cancellationToken);

            // Revalidate authoritative SQL state
            // inside the write transaction.

            var trip = await _db.Trips
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x => x.Id == invitation.TripPlanId,
                    cancellationToken);

            if (trip is null)
            {
                throw new KeyNotFoundException(
                    "Trip plan was not found.");
            }

            if (!string.Equals(
                    trip.OwnerUserId,
                    invitation.SenderUserId,
                    StringComparison.Ordinal))
            {
                throw new UnauthorizedAccessException(
                    "Only the trip owner can invite.");
            }

            if (trip.DraftStatus == TripDraftStatus.Cancelled)
            {
                throw new InvalidOperationException(
                    "Cancelled trips cannot send invitations.");
            }

            var account = await _db.Users
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x => x.UserId == invitation.RecipientUserId,
                    cancellationToken);

            if (account is null ||
                account.Status != UserAccountStatus.Active ||
                !account.VerifiedAtUtc.HasValue ||
                account.DeletionRequestedAtUtc.HasValue ||
                account.CreatedAtUtc == default ||
                account.CreatedAtUtc.Offset != TimeSpan.Zero ||
                account.VerifiedAtUtc.Value.Offset != TimeSpan.Zero ||
                account.VerifiedAtUtc.Value < account.CreatedAtUtc)
            {
                throw new InvalidOperationException(
                    "Recipient account is not active.");
            }

            var activeMemberExists =
                await _db.TripMembers
                    .AsNoTracking()
                    .AnyAsync(
                        x =>
                            x.TripPlanId == invitation.TripPlanId &&
                            x.MemberUserId ==
                                invitation.RecipientUserId &&
                            x.IsActive,
                        cancellationToken);

            if (activeMemberExists)
            {
                throw new InvalidOperationException(
                    "Recipient is already an active member.");
            }

            var openInvitationExists =
                await _db.TripInvitations
                    .AsNoTracking()
                    .AnyAsync(
                        x =>
                            x.TripPlanId == invitation.TripPlanId &&
                            x.RecipientUserId ==
                                invitation.RecipientUserId &&
                            (x.Status == TripInvitationStatus.Pending ||
                             x.Status == TripInvitationStatus.Accepted),
                        cancellationToken);

            if (openInvitationExists)
            {
                throw new InvalidOperationException(
                    "An open or accepted invitation already exists.");
            }

            _db.TripInvitations.Add(invitation);

            try
            {
                await _db.SaveChangesAsync(
                    cancellationToken);

                await transaction.CommitAsync(
                    cancellationToken);
            }
            catch (DbUpdateException exception)
            {
                throw new InvalidOperationException(
                    "Invitation creation failed due to a " +
                    "duplicate or database constraint conflict.",
                    exception);
            }
        });
    }

    public async Task<IReadOnlyList<TripInvitation>>
        GetPendingForRecipientAsync(
            string recipientUserId,
            CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(recipientUserId))
        {
            throw new ArgumentException(
                "Recipient user ID is required.");
        }

        return await _db.TripInvitations
            .AsNoTracking()
            .Where(x =>
                x.RecipientUserId == recipientUserId &&
                x.Status == TripInvitationStatus.Pending)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }
}
