
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.TripSafety.Interfaces;
using Wayhunter.Domain.Modules.TripSafety.Entities;
using Wayhunter.Domain.Modules.TripSafety.Enums;

namespace Wayhunter.Infrastructure.Persistence.Repositories.TripSafety;

public sealed class InMemoryTripInvitationDecisionRepository
    : ITripInvitationDecisionRepository
{
    private readonly object _sync = new();

    private readonly Dictionary<Guid, TripInvitation>
        _invitations = new();

    private readonly Dictionary<Guid, TripMember>
        _membersByInvitationId = new();

    // Add a pending invitation for in-memory testing.
    // Production persistence will be implemented separately.

    public Task AddAsync(
        TripInvitation invitation,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(invitation);

        if (invitation.Status != TripInvitationStatus.Pending)
        {
            throw new InvalidOperationException(
                "Only pending invitations can be added.");
        }

        lock (_sync)
        {
            if (_invitations.ContainsKey(invitation.Id))
            {
                throw new InvalidOperationException(
                    "Invitation ID already exists.");
            }

            bool hasOpenInvitation = _invitations.Values.Any(x =>
                x.TripPlanId == invitation.TripPlanId &&
                string.Equals(
                    x.RecipientUserId,
                    invitation.RecipientUserId,
                    StringComparison.Ordinal) &&
                (x.Status == TripInvitationStatus.Pending ||
                 x.Status == TripInvitationStatus.Accepted));

            if (hasOpenInvitation)
            {
                throw new InvalidOperationException(
                    "An open or accepted invitation already exists.");
            }

            _invitations.Add(
                invitation.Id,
                CloneInvitation(invitation));
        }

        return Task.CompletedTask;
    }

    // Return a separate domain instance.
    // Callers must not mutate persisted state directly.

    public Task<TripInvitation?> GetByIdAsync(
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

        lock (_sync)
        {
            if (!_invitations.TryGetValue(
                invitationId,
                out var invitation))
            {
                return Task.FromResult<TripInvitation?>(null);
            }

            return Task.FromResult<TripInvitation?>(
                CloneInvitation(invitation));
        }
    }

    public Task<TripMember?> GetMemberByInvitationIdAsync(
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

        lock (_sync)
        {
            _membersByInvitationId.TryGetValue(
                invitationId,
                out var member);

            return Task.FromResult(member);
        }
    }

    // Invitation decision and membership are saved
    // together under one lock.
    //
    // A stale competing decision is rejected.
    // The application service must reload the
    // persisted result when handling a concurrent
    // duplicate acceptance.
    //
    // No GPS consent is created.

    public Task SaveDecisionAsync(
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

        lock (_sync)
        {
            if (!_invitations.TryGetValue(
                invitation.Id,
                out var stored))
            {
                throw new KeyNotFoundException(
                    "Trip invitation was not found.");
            }

            // Prevent a changed or unrelated invitation
            // from overwriting the stored record.

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

            // Compare-and-save protection for
            // simultaneous Accept / Reject / Cancel.

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

            if (invitation.Status == TripInvitationStatus.Accepted)
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
                        invitation.RespondedAtUtc.Value)
                {
                    throw new InvalidOperationException(
                        "Membership does not match invitation.");
                }

                if (_membersByInvitationId.ContainsKey(
                    invitation.Id))
                {
                    throw new InvalidOperationException(
                        "Invitation already has a membership.");
                }

                bool duplicateMember =
                    _membersByInvitationId.Values.Any(x =>
                        x.TripPlanId == invitation.TripPlanId &&
                        string.Equals(
                            x.MemberUserId,
                            invitation.RecipientUserId,
                            StringComparison.Ordinal) &&
                        x.IsActive);

                if (duplicateMember)
                {
                    throw new InvalidOperationException(
                        "User is already an active trip member.");
                }
            }
            else if (invitation.Status ==
                         TripInvitationStatus.Rejected ||
                     invitation.Status ==
                         TripInvitationStatus.Cancelled)
            {
                if (memberToAdd is not null)
                {
                    throw new InvalidOperationException(
                        "Rejected or cancelled invitation cannot add membership.");
                }
            }
            else
            {
                throw new InvalidOperationException(
                    "Unsupported invitation decision.");
            }

            // All checks passed.
            // Commit both records inside the same lock.

            var snapshot = CloneInvitation(invitation);

            if (memberToAdd is not null)
            {
                _membersByInvitationId.Add(
                    invitation.Id,
                    memberToAdd);
            }

            _invitations[invitation.Id] = snapshot;
        }

        return Task.CompletedTask;
    }

    private static TripInvitation CloneInvitation(
        TripInvitation source)
    {
        var copy = TripInvitation.Create(
            source.Id,
            source.TripPlanId,
            source.SenderUserId,
            source.RecipientUserId,
            source.CreatedAtUtc);

        if (source.Status == TripInvitationStatus.Pending)
        {
            return copy;
        }

        if (!source.RespondedAtUtc.HasValue)
        {
            throw new InvalidOperationException(
                "Decided invitation must have response time.");
        }

        var respondedAtUtc = source.RespondedAtUtc.Value;

        switch (source.Status)
        {
            case TripInvitationStatus.Accepted:
                copy.Accept(respondedAtUtc);
                break;

            case TripInvitationStatus.Rejected:
                copy.Reject(respondedAtUtc);
                break;

            case TripInvitationStatus.Cancelled:
                copy.Cancel(respondedAtUtc);
                break;

            default:
                throw new InvalidOperationException(
                    "Unknown invitation status.");
        }

        return copy;
    }
}
