
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Wayhunter.Domain.Modules.TripSafety.Entities;

namespace Wayhunter.Application.Modules.TripSafety.Interfaces;

public interface ITripInvitationCreateStore
{
    // Atomically persist a new pending invitation.
    // Reject duplicate pending/accepted invitations.
    Task CreateAsync(
        TripInvitation invitation,
        CancellationToken cancellationToken = default);

    // Only the authenticated recipient may request
    // their own pending invitations.
    Task<IReadOnlyList<TripInvitation>>
        GetPendingForRecipientAsync(
            string recipientUserId,
            CancellationToken cancellationToken = default);
}
