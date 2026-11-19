
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Wayhunter.Application.Modules.TripSafety.Interfaces;
using Wayhunter.Application.Modules.TripSafety.Validators;

using Wayhunter.Domain.Modules.TripSafety.Entities;

namespace Wayhunter.Application.Modules.TripSafety.Services;

public sealed class TripInvitationCreateService
{
    private readonly ITripMembershipRevocationRepository _membership;
    private readonly ITripInvitationCreateStore _creation;
    private readonly ITripInviteEligibilityAccess _eligibility;
    private readonly TimeProvider _clock;

    public TripInvitationCreateService(
        ITripMembershipRevocationRepository membership,
        ITripInvitationCreateStore creation,
        ITripInviteEligibilityAccess eligibility,
        TimeProvider clock)
    {
        _membership = membership
            ?? throw new ArgumentNullException(nameof(membership));

        _creation = creation
            ?? throw new ArgumentNullException(nameof(creation));

        _eligibility = eligibility
            ?? throw new ArgumentNullException(nameof(eligibility));

        _clock = clock
            ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<TripInvitation> CreateAsync(
        Guid tripPlanId,
        string? authenticatedSenderUserId,
        string? recipientUserId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (tripPlanId == Guid.Empty)
        {
            throw new ArgumentException(
                "Valid trip plan ID is required.");
        }

        var sender = RequireCanonicalUserId(
            authenticatedSenderUserId,
            trusted: true);

        var recipient = RequireCanonicalUserId(
            recipientUserId,
            trusted: false);

        // Verify the trip exists and the authenticated
        // sender is its actual owner.

        var trip = await _membership.GetTripPlanByIdAsync(
            tripPlanId,
            cancellationToken);

        if (trip is null)
        {
            throw new KeyNotFoundException(
                "Trip plan was not found.");
        }

        if (!string.Equals(
            trip.OwnerUserId,
            sender,
            StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException(
                "Only the trip owner may invite users.");
        }

        if (string.Equals(
            sender,
            recipient,
            StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Trip owners cannot invite themselves.");
        }

        // Check existing active membership.

        var existingMember =
            await _membership.GetMemberAsync(
                tripPlanId,
                recipient,
                cancellationToken);

        // Authoritative server-side recipient validation.
        // The eligibility reader must fail closed
        // if M1 blocked-relationship data is unavailable.

        var recipientState =
            await _eligibility.CheckAsync(
                sender,
                recipient,
                cancellationToken);

        // Existing domain validator checks:
        // - Trip owner
        // - Cancelled trip
        // - Active recipient
        // - Blocked relationship
        // - Active membership
        //
        // Duplicate invitations are checked atomically
        // in the persistence store.

        TripInviteEligibilityValidator.EnsureCanInvite(
            trip,
            sender,
            recipient,
            recipientState.RecipientIsActive,
            recipientState.IsBlocked,
            existingInvitation: null,
            existingMember: existingMember);

        var invitation = TripInvitation.Create(
            Guid.NewGuid(),
            tripPlanId,
            sender,
            recipient,
            _clock.GetUtcNow());

        await _creation.CreateAsync(
            invitation,
            cancellationToken);

        // Creating an invitation does not grant
        // GPS consent or create membership.

        return invitation;
    }

    public Task<IReadOnlyList<TripInvitation>>
        GetMyPendingAsync(
            string? authenticatedRecipientUserId,
            CancellationToken cancellationToken = default)
    {
        var recipient = RequireCanonicalUserId(
            authenticatedRecipientUserId,
            trusted: true);

        return _creation.GetPendingForRecipientAsync(
            recipient,
            cancellationToken);
    }

    private static string RequireCanonicalUserId(
        string? value,
        bool trusted)
    {
        if (!Guid.TryParseExact(
                value,
                "D",
                out var id) ||
            id == Guid.Empty)
        {
            if (trusted)
            {
                throw new UnauthorizedAccessException(
                    "Trusted M1 user identity is required.");
            }

            throw new ArgumentException(
                "Recipient user ID must be a valid GUID.");
        }

        return id.ToString("D");
    }
}
