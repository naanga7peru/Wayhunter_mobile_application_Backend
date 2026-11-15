
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.TripSafety.Interfaces;
using Wayhunter.Domain.Modules.TravelPlanning.Entities;
using Wayhunter.Domain.Modules.TripSafety.Entities;

namespace Wayhunter.Infrastructure.Persistence.Repositories.TripSafety;

public sealed record TripMembershipRevocationNotice(
    Guid TripPlanId,
    Guid MemberId,
    string MemberUserId,
    DateTimeOffset RemovedAtUtc);

public sealed class InMemoryTripMembershipRevocationRepository
    : ITripMembershipRevocationRepository
{
    private readonly object _sync = new();

    private readonly Dictionary<Guid, TripPlan> _trips = new();

    private readonly Dictionary<Guid, TripMember> _members = new();

    private readonly Dictionary<Guid, TripMembershipRevocationNotice>
        _notices = new();

    // Test/demo setup only. Not production SQL persistence.

    public Task AddTripAsync(
        TripPlan trip,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(trip);

        lock (_sync)
        {
            if (_trips.ContainsKey(trip.Id))
            {
                throw new InvalidOperationException(
                    "Trip already exists.");
            }

            _trips.Add(trip.Id, trip);
        }

        return Task.CompletedTask;
    }

    public Task AddMemberAsync(
        TripMember member,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(member);

        lock (_sync)
        {
            if (!_trips.TryGetValue(
                member.TripPlanId,
                out var trip))
            {
                throw new KeyNotFoundException(
                    "Trip plan was not found.");
            }

            if (!member.IsActive)
            {
                throw new InvalidOperationException(
                    "Only active members can be initially added.");
            }

            if (string.Equals(
                trip.OwnerUserId,
                member.MemberUserId,
                StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Trip owner cannot be added as a member.");
            }

            if (_members.ContainsKey(member.Id))
            {
                throw new InvalidOperationException(
                    "Member ID already exists.");
            }

            bool duplicate = _members.Values.Any(x =>
                x.TripPlanId == member.TripPlanId &&
                x.IsActive &&
                string.Equals(
                    x.MemberUserId,
                    member.MemberUserId,
                    StringComparison.Ordinal));

            if (duplicate)
            {
                throw new InvalidOperationException(
                    "User is already an active trip member.");
            }

            _members.Add(
                member.Id,
                CloneMember(member, trip.OwnerUserId));
        }

        return Task.CompletedTask;
    }

    public Task<TripPlan?> GetTripPlanByIdAsync(
        Guid tripPlanId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_sync)
        {
            _trips.TryGetValue(tripPlanId, out var trip);
            return Task.FromResult(trip);
        }
    }

    public Task<TripMember?> GetMemberAsync(
        Guid tripPlanId,
        string memberUserId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(memberUserId))
        {
            throw new ArgumentException(
                "Member User ID is required.",
                nameof(memberUserId));
        }

        lock (_sync)
        {
            var member = _members.Values
                .Where(x =>
                    x.TripPlanId == tripPlanId &&
                    string.Equals(
                        x.MemberUserId,
                        memberUserId.Trim(),
                        StringComparison.Ordinal))
                .OrderByDescending(x => x.IsActive)
                .ThenByDescending(x => x.JoinedAtUtc)
                .FirstOrDefault();

            if (member is null)
            {
                return Task.FromResult<TripMember?>(null);
            }

            if (!_trips.TryGetValue(
                tripPlanId,
                out var trip))
            {
                throw new InvalidOperationException(
                    "Member references a missing trip.");
            }

            return Task.FromResult<TripMember?>(
                CloneMember(member, trip.OwnerUserId));
        }
    }

    public Task<bool> RemoveMemberAndQueueNotificationAsync(
        Guid tripPlanId,
        Guid memberId,
        DateTimeOffset removedAtUtc,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (tripPlanId == Guid.Empty)
            throw new ArgumentException(
                "Trip Plan ID is required.",
                nameof(tripPlanId));

        if (memberId == Guid.Empty)
            throw new ArgumentException(
                "Member ID is required.",
                nameof(memberId));

        if (removedAtUtc == default ||
            removedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "Valid UTC removal time is required.",
                nameof(removedAtUtc));
        }

        lock (_sync)
        {
            if (!_members.TryGetValue(
                memberId,
                out var member) ||
                member.TripPlanId != tripPlanId)
            {
                throw new KeyNotFoundException(
                    "Member was not found in this trip.");
            }

            // A repeated removal must have no side effects.

            if (!member.IsActive)
            {
                return Task.FromResult(false);
            }

            if (_notices.ContainsKey(memberId))
            {
                throw new InvalidOperationException(
                    "A revocation notice already exists.");
            }

            // Check before mutating anything.

            if (removedAtUtc < member.JoinedAtUtc)
            {
                throw new InvalidOperationException(
                    "Removal cannot precede joining.");
            }

            var notice = new TripMembershipRevocationNotice(
                tripPlanId,
                memberId,
                member.MemberUserId,
                removedAtUtc);

            // One lock protects both changes.
            // No external notification is sent here.

            member.Remove(removedAtUtc);
            _notices.Add(memberId, notice);

            return Task.FromResult(true);
        }
    }

    // Inspect the in-memory notification queue.
    // Real delivery/outbox persistence is pending.

    public Task<IReadOnlyList<TripMembershipRevocationNotice>>
        GetQueuedNoticesAsync(
            CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_sync)
        {
            IReadOnlyList<TripMembershipRevocationNotice> result =
                _notices.Values
                    .OrderBy(x => x.RemovedAtUtc)
                    .ToArray();

            return Task.FromResult(result);
        }
    }

    private static TripMember CloneMember(
        TripMember source,
        string ownerUserId)
    {
        // Reconstruct a separate domain instance so callers
        // cannot mutate the stored member directly.

        var invitation = TripInvitation.Create(
            source.InvitationId,
            source.TripPlanId,
            ownerUserId,
            source.MemberUserId,
            source.JoinedAtUtc);

        invitation.Accept(source.JoinedAtUtc);

        var copy = TripMember.FromAcceptedInvitation(
            source.Id,
            invitation);

        if (!source.IsActive)
        {
            if (!source.RemovedAtUtc.HasValue)
            {
                throw new InvalidOperationException(
                    "Inactive member lacks removal timestamp.");
            }

            copy.Remove(source.RemovedAtUtc.Value);
        }

        return copy;
    }
}
