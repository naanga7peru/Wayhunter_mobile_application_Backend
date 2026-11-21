
using System;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;

using Wayhunter.Api.Contracts;

using Wayhunter.Application.Modules.TripSafety.Interfaces;

using Wayhunter.Infrastructure.Persistence.DbContexts;

namespace Wayhunter.Api.Services.TripSafety;

// =====================================================
// M3-MS03-T02
// Default eligibility reader: FAIL CLOSED
// =====================================================

public sealed class UnavailableTripInviteEligibilityAccess
    : ITripInviteEligibilityAccess
{
    public Task<TripInviteRecipientEligibility> CheckAsync(
        string senderUserId,
        string recipientUserId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        throw new TripInvitePrerequisiteUnavailableException(
            "Invitation creation requires authoritative " +
            "M1 account and blocked-relationship services.");
    }
}

// =====================================================
// M1 Block Relationship Contract
// =====================================================
//
// M1 must supply a real server-side implementation.
// This fallback must never return false because
// that would silently bypass blocked-user checks.

public sealed class UnconfiguredTripInviteBlockRelationshipReader
    : ITripInviteBlockRelationshipReader
{
    public Task<bool> IsBlockedEitherWayAsync(
        string senderUserId,
        string recipientUserId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        throw new TripInvitePrerequisiteUnavailableException(
            "M1 authoritative blocked-relationship " +
            "reader is not configured.");
    }
}

// =====================================================
// SQL Trip Invitation Eligibility
// =====================================================
//
// Validates:
// 1. Recipient exists in M1 UserAccount table
// 2. Recipient is active and verified
// 3. Recipient has not requested deletion
// 4. Sender/recipient blocking in either direction
//
// Does not grant GPS consent or create membership.

public sealed class SqlTripInviteEligibilityAccess
    : ITripInviteEligibilityAccess
{
    private readonly WayhunterSharedDbContext _db;

    private readonly ITripInviteBlockRelationshipReader _blocks;

    public SqlTripInviteEligibilityAccess(
        WayhunterSharedDbContext db,
        ITripInviteBlockRelationshipReader blocks)
    {
        _db = db
            ?? throw new ArgumentNullException(nameof(db));

        _blocks = blocks
            ?? throw new ArgumentNullException(nameof(blocks));
    }

    public async Task<TripInviteRecipientEligibility> CheckAsync(
        string senderUserId,
        string recipientUserId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(senderUserId))
        {
            throw new ArgumentException(
                "Sender User ID is required.",
                nameof(senderUserId));
        }

        if (string.IsNullOrWhiteSpace(recipientUserId))
        {
            throw new ArgumentException(
                "Recipient User ID is required.",
                nameof(recipientUserId));
        }

        var sender = senderUserId.Trim();
        var recipient = recipientUserId.Trim();

        // Read authoritative M1 account information.

        var account = await _db.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.UserId == recipient,
                cancellationToken);

        // Reuse the approved M1 trusted account check.

        var recipientIsActive =
            TrustedIdentityAccessContract.IsActiveAccountOwner(
                recipient,
                account);

        if (!recipientIsActive)
        {
            return new TripInviteRecipientEligibility(
                RecipientIsActive: false,
                IsBlocked: false);
        }

        // Check authoritative server-side block relationship.
        // If M1 reader is unavailable, it throws
        // and prevents invitation creation.

        var isBlocked =
            await _blocks.IsBlockedEitherWayAsync(
                sender,
                recipient,
                cancellationToken);

        return new TripInviteRecipientEligibility(
            RecipientIsActive: true,
            IsBlocked: isBlocked);
    }
}
