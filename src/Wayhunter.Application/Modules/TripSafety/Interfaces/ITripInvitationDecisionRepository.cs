
using System;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Domain.Modules.TripSafety.Entities;

namespace Wayhunter.Application.Modules.TripSafety.Interfaces;

public interface ITripInvitationDecisionRepository
{
    // Retrieve an invitation by its unique ID.
    // Returns null if it does not exist.

    Task<TripInvitation?> GetByIdAsync(
        Guid invitationId,
        CancellationToken cancellationToken = default);

    // Retrieve membership created from an invitation.
    // Used to prevent duplicate membership on
    // repeated Accept requests.

    Task<TripMember?> GetMemberByInvitationIdAsync(
        Guid invitationId,
        CancellationToken cancellationToken = default);

    // Persist a validated invitation state transition.
    //
    // For Accept:
    // - Save Accepted status and membership atomically.
    // - Do not create duplicate membership.
    //
    // For Reject / Cancel:
    // - Save the invitation status only.
    // - memberToAdd must be null.
    //
    // Implementation requirements:
    // - Atomic transaction for invitation + membership.
    // - Uniqueness for InvitationId and active membership.
    // - Safe handling of concurrent requests.
    // - No automatic GPS consent.
    //
    // Authorization and domain state validation
    // must occur before invoking this method.

    Task SaveDecisionAsync(
        TripInvitation invitation,
        TripMember? memberToAdd,
        CancellationToken cancellationToken = default);
}
