
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Wayhunter.Application.Modules.TripSafety.Interfaces;

using Wayhunter.Domain.Modules.TripSafety.Entities;
using Wayhunter.Domain.Modules.TripSafety.Enums;

namespace Wayhunter.Infrastructure.Persistence.Repositories.TripSafety;

public sealed class InMemoryTripInvitationCreateStore
    : ITripInvitationCreateStore
{
    private readonly InMemoryTripInvitationDecisionRepository _decisions;

    private readonly object _sync = new();

    private readonly HashSet<Guid> _createdIds = new();

    public InMemoryTripInvitationCreateStore(
        InMemoryTripInvitationDecisionRepository decisions)
    {
        _decisions = decisions
            ?? throw new ArgumentNullException(nameof(decisions));
    }

    public async Task CreateAsync(
        TripInvitation invitation,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ArgumentNullException.ThrowIfNull(invitation);

        // Existing AddAsync uses a lock and prevents
        // duplicate pending or accepted invitations.

        await _decisions.AddAsync(
            invitation,
            cancellationToken);

        lock (_sync)
        {
            _createdIds.Add(invitation.Id);
        }
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

        Guid[] ids;

        lock (_sync)
        {
            ids = _createdIds.ToArray();
        }

        var pending = new List<TripInvitation>();

        foreach (var id in ids)
        {
            var item = await _decisions.GetByIdAsync(
                id,
                cancellationToken);

            if (item is not null &&
                item.Status == TripInvitationStatus.Pending &&
                string.Equals(
                    item.RecipientUserId,
                    recipientUserId,
                    StringComparison.Ordinal))
            {
                pending.Add(item);
            }
        }

        return pending
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToArray();
    }
}
