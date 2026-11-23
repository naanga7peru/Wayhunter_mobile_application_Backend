
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.TripSafety.Interfaces;
using Wayhunter.Domain.Modules.TripSafety.Entities;
using Wayhunter.Domain.Modules.TripSafety.Enums;

namespace Wayhunter.Application.Modules.TripSafety.Services;

public sealed class TripInvitationDecisionService
{
    private readonly ITripInvitationDecisionRepository _repository;
    private readonly TimeProvider _timeProvider;
    private readonly ITripInviteBlockRelationshipReader _blockReader;

    public TripInvitationDecisionService(
        ITripInvitationDecisionRepository repository,
        TimeProvider timeProvider,
        ITripInviteBlockRelationshipReader blockReader)
    {
        _repository = repository
            ?? throw new ArgumentNullException(nameof(repository));

        _timeProvider = timeProvider
            ?? throw new ArgumentNullException(nameof(timeProvider));

        _blockReader = blockReader
            ?? throw new ArgumentNullException(nameof(blockReader));
    }

    // Only the invited recipient may accept.
    // Repeated Accept returns the existing member.
    // Accept never grants GPS sharing consent.

    public async Task<TripMember> AcceptAsync(
        Guid invitationId,
        string? authenticatedUserId,
        CancellationToken cancellationToken = default)
    {
        string userId = RequireAuthenticatedUser(
            authenticatedUserId);

        var invitation = await GetRequiredInvitationAsync(
            invitationId,
            cancellationToken);

        EnsureAuthorized(
            invitation.RecipientUserId,
            userId);

        // Sequential repeated Accept.
        if (invitation.Status == TripInvitationStatus.Accepted)
        {
            return await GetRequiredMemberAsync(
                invitation,
                cancellationToken);
        }

        if (await _blockReader.IsBlockedEitherWayAsync(
                invitation.SenderUserId,
                invitation.RecipientUserId,
                cancellationToken))
        {
            throw new UnauthorizedAccessException(
                "Blocked users cannot accept invitations.");
        }

        if (invitation.Status != TripInvitationStatus.Pending)
        {
            throw new InvalidOperationException(
                "Only pending invitations can be accepted.");
        }

        invitation.Accept(_timeProvider.GetUtcNow());

        var member = TripMember.FromAcceptedInvitation(
            Guid.NewGuid(),
            invitation);

        try
        {
            // Repository must atomically save both records.
            await _repository.SaveDecisionAsync(
                invitation,
                member,
                cancellationToken);

            return member;
        }
        catch (InvalidOperationException)
            when (!cancellationToken.IsCancellationRequested)
        {
            // Concurrent Accept recovery:
            // Another request may have accepted first.
            //
            // Do not create a second membership.
            // Reload the committed result.

            var storedInvitation =
                await _repository.GetByIdAsync(
                    invitationId,
                    cancellationToken);

            if (storedInvitation is null ||
                storedInvitation.Status !=
                    TripInvitationStatus.Accepted)
            {
                // The failure was not a successful
                // competing Accept.
                throw;
            }

            var storedMember =
                await _repository.GetMemberByInvitationIdAsync(
                    invitationId,
                    cancellationToken);

            if (storedMember is null ||
                storedMember.InvitationId != invitationId ||
                storedMember.TripPlanId !=
                    storedInvitation.TripPlanId ||
                !string.Equals(
                    storedMember.MemberUserId,
                    storedInvitation.RecipientUserId,
                    StringComparison.Ordinal))
            {
                throw;
            }

            return storedMember;
        }
    }

    // Only the recipient may reject.
    // Repeated Reject is idempotent.

    public async Task<TripInvitation> RejectAsync(
        Guid invitationId,
        string? authenticatedUserId,
        CancellationToken cancellationToken = default)
    {
        string userId = RequireAuthenticatedUser(
            authenticatedUserId);

        var invitation = await GetRequiredInvitationAsync(
            invitationId,
            cancellationToken);

        EnsureAuthorized(
            invitation.RecipientUserId,
            userId);

        if (invitation.Status == TripInvitationStatus.Rejected)
        {
            return invitation;
        }

        if (invitation.Status != TripInvitationStatus.Pending)
        {
            throw new InvalidOperationException(
                "Only pending invitations can be rejected.");
        }

        invitation.Reject(_timeProvider.GetUtcNow());

        try
        {
            await _repository.SaveDecisionAsync(
                invitation,
                null,
                cancellationToken);

            return invitation;
        }
        catch (InvalidOperationException)
            when (!cancellationToken.IsCancellationRequested)
        {
            // Recover a concurrent repeated Reject.

            var stored = await _repository.GetByIdAsync(
                invitationId,
                cancellationToken);

            if (stored is not null &&
                stored.Status == TripInvitationStatus.Rejected)
            {
                return stored;
            }

            throw;
        }
    }

    // Only the sender may cancel.
    // Repeated Cancel is idempotent.

    public async Task<TripInvitation> CancelAsync(
        Guid invitationId,
        string? authenticatedUserId,
        CancellationToken cancellationToken = default)
    {
        string userId = RequireAuthenticatedUser(
            authenticatedUserId);

        var invitation = await GetRequiredInvitationAsync(
            invitationId,
            cancellationToken);

        EnsureAuthorized(
            invitation.SenderUserId,
            userId);

        if (invitation.Status == TripInvitationStatus.Cancelled)
        {
            return invitation;
        }

        if (invitation.Status != TripInvitationStatus.Pending)
        {
            throw new InvalidOperationException(
                "Only pending invitations can be cancelled.");
        }

        invitation.Cancel(_timeProvider.GetUtcNow());

        try
        {
            await _repository.SaveDecisionAsync(
                invitation,
                null,
                cancellationToken);

            return invitation;
        }
        catch (InvalidOperationException)
            when (!cancellationToken.IsCancellationRequested)
        {
            // Recover a concurrent repeated Cancel.

            var stored = await _repository.GetByIdAsync(
                invitationId,
                cancellationToken);

            if (stored is not null &&
                stored.Status == TripInvitationStatus.Cancelled)
            {
                return stored;
            }

            throw;
        }
    }

    private async Task<TripInvitation> GetRequiredInvitationAsync(
        Guid invitationId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (invitationId == Guid.Empty)
        {
            throw new ArgumentException(
                "Valid Invitation ID is required.",
                nameof(invitationId));
        }

        var invitation = await _repository.GetByIdAsync(
            invitationId,
            cancellationToken);

        if (invitation is null)
        {
            throw new KeyNotFoundException(
                "Trip invitation was not found.");
        }

        return invitation;
    }

    private async Task<TripMember> GetRequiredMemberAsync(
        TripInvitation invitation,
        CancellationToken cancellationToken)
    {
        var member =
            await _repository.GetMemberByInvitationIdAsync(
                invitation.Id,
                cancellationToken);

        if (member is null ||
            member.InvitationId != invitation.Id ||
            member.TripPlanId != invitation.TripPlanId ||
            !string.Equals(
                member.MemberUserId,
                invitation.RecipientUserId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Accepted invitation has no valid membership record.");
        }

        return member;
    }

    private static string RequireAuthenticatedUser(
        string? authenticatedUserId)
    {
        if (string.IsNullOrWhiteSpace(authenticatedUserId))
        {
            throw new UnauthorizedAccessException(
                "Authenticated user is required.");
        }

        // The user ID must come from trusted
        // backend authentication, not the client.

        return authenticatedUserId.Trim();
    }

    private static void EnsureAuthorized(
        string expectedUserId,
        string authenticatedUserId)
    {
        if (!string.Equals(
            expectedUserId,
            authenticatedUserId,
            StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException(
                "User is not authorized for this invitation.");
        }
    }
}
