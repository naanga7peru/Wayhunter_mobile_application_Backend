using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Wayhunter.Application.Modules.TripSafety.Interfaces;
using Wayhunter.Domain.Modules.TripSafety.Entities;
using Wayhunter.Domain.Modules.TripSafety.Enums;
using Wayhunter.Infrastructure.Persistence.DbContexts;

namespace Wayhunter.Infrastructure.Persistence.Repositories.TripSafety;

public sealed class EfTripInvitationDecisionRepository
    : ITripInvitationDecisionRepository
{
    private readonly WayhunterSharedDbContext _dbContext;

    public EfTripInvitationDecisionRepository(
        WayhunterSharedDbContext dbContext)
    {
        _dbContext = dbContext
            ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<TripInvitation?> GetByIdAsync(
        Guid invitationId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (invitationId == Guid.Empty)
        {
            throw new ArgumentException(
                "Valid Invitation ID is required.",
                nameof(invitationId));
        }

        return await _dbContext.TripInvitations
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Id == invitationId,
                cancellationToken);
    }

    public async Task<TripMember?> GetMemberByInvitationIdAsync(
        Guid invitationId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (invitationId == Guid.Empty)
        {
            throw new ArgumentException(
                "Valid Invitation ID is required.",
                nameof(invitationId));
        }

        return await _dbContext.TripMembers
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.InvitationId == invitationId,
                cancellationToken);
    }

    public async Task SaveDecisionAsync(
        TripInvitation invitation,
        TripMember? memberToAdd,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(invitation);

        if (!invitation.RespondedAtUtc.HasValue)
        {
            throw new InvalidOperationException(
                "Invitation decision timestamp is required.");
        }

        if (invitation.Status == TripInvitationStatus.Pending)
        {
            throw new InvalidOperationException(
                "A pending invitation has no decision to save.");
        }

        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

        var stored =
            await _dbContext.TripInvitations
                .SingleOrDefaultAsync(
                    x => x.Id == invitation.Id,
                    cancellationToken);

        if (stored is null)
        {
            throw new KeyNotFoundException(
                "Trip invitation was not found.");
        }

        if (stored.TripPlanId != invitation.TripPlanId ||
            stored.CreatedAtUtc != invitation.CreatedAtUtc ||
            !string.Equals(
                stored.SenderUserId,
                invitation.SenderUserId,
                StringComparison.Ordinal) ||
            !string.Equals(
                stored.RecipientUserId,
                invitation.RecipientUserId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Invitation details do not match stored data.");
        }

        if (stored.Status != TripInvitationStatus.Pending)
        {
            throw new InvalidOperationException(
                "Invitation decision has already been saved.");
        }

        if (invitation.RespondedAtUtc.Value <
            stored.CreatedAtUtc)
        {
            throw new InvalidOperationException(
                "Decision time cannot precede creation.");
        }

        switch (invitation.Status)
        {
            case TripInvitationStatus.Accepted:
                await ValidateAcceptedDecisionAsync(
                    invitation,
                    memberToAdd,
                    cancellationToken);
                break;

            case TripInvitationStatus.Rejected:
            case TripInvitationStatus.Cancelled:
                if (memberToAdd is not null)
                {
                    throw new InvalidOperationException(
                        "Rejected or cancelled invitation cannot add membership.");
                }

                break;

            default:
                throw new InvalidOperationException(
                    "Unsupported invitation decision.");
        }

        switch (invitation.Status)
        {
            case TripInvitationStatus.Accepted:
                stored.Accept(
                    invitation.RespondedAtUtc.Value);
                break;

            case TripInvitationStatus.Rejected:
                stored.Reject(
                    invitation.RespondedAtUtc.Value);
                break;

            case TripInvitationStatus.Cancelled:
                stored.Cancel(
                    invitation.RespondedAtUtc.Value);
                break;
        }

        if (memberToAdd is not null)
        {
            _dbContext.TripMembers.Add(memberToAdd);
        }

        try
        {
            await _dbContext.SaveChangesAsync(
                cancellationToken);

            await transaction.CommitAsync(
                cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            await transaction.RollbackAsync(
                cancellationToken);

            throw new InvalidOperationException(
                "Invitation decision could not be saved because a conflicting decision or membership already exists.",
                exception);
        }
    }

    private async Task ValidateAcceptedDecisionAsync(
        TripInvitation invitation,
        TripMember? memberToAdd,
        CancellationToken cancellationToken)
    {
        if (memberToAdd is null)
        {
            throw new InvalidOperationException(
                "Accepted invitation requires membership.");
        }

        if (memberToAdd.InvitationId != invitation.Id ||
            memberToAdd.TripPlanId != invitation.TripPlanId ||
            !string.Equals(
                memberToAdd.MemberUserId,
                invitation.RecipientUserId,
                StringComparison.Ordinal) ||
            memberToAdd.JoinedAtUtc !=
                invitation.RespondedAtUtc!.Value)
        {
            throw new InvalidOperationException(
                "Membership does not match invitation.");
        }

        var invitationAlreadyHasMember =
            await _dbContext.TripMembers
                .AsNoTracking()
                .AnyAsync(
                    x => x.InvitationId == invitation.Id,
                    cancellationToken);

        if (invitationAlreadyHasMember)
        {
            throw new InvalidOperationException(
                "Invitation already has a membership.");
        }

        var activeMembershipExists =
            await _dbContext.TripMembers
                .AsNoTracking()
                .AnyAsync(
                    x =>
                        x.TripPlanId == invitation.TripPlanId &&
                        x.MemberUserId ==
                            invitation.RecipientUserId &&
                        x.IsActive,
                    cancellationToken);

        if (activeMembershipExists)
        {
            throw new InvalidOperationException(
                "User is already an active trip member.");
        }
    }
}
