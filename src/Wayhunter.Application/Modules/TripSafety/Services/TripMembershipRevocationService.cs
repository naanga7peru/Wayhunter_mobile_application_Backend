
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.TripSafety.Interfaces;
using Wayhunter.Domain.Modules.TripSafety.Entities;

namespace Wayhunter.Application.Modules.TripSafety.Services;

public sealed class TripMembershipRevocationService
{
    private readonly ITripMembershipRevocationRepository _repository;
    private readonly TimeProvider _timeProvider;

    public TripMembershipRevocationService(
        ITripMembershipRevocationRepository repository,
        TimeProvider timeProvider)
    {
        _repository = repository
            ?? throw new ArgumentNullException(nameof(repository));

        _timeProvider = timeProvider
            ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    // Only the authenticated trip owner may remove a member.
    //
    // Returns:
    // true  - member removed and notification queued.
    // false - member already inactive.
    //
    // Authentication must come from trusted backend claims.
    // The repository must perform an atomic conditional update
    // and enqueue the notification exactly once.

    public async Task<bool> RemoveAsync(
        Guid tripPlanId,
        string? authenticatedUserId,
        string? memberUserId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Step 1: Validate authenticated identity.

        if (string.IsNullOrWhiteSpace(authenticatedUserId))
        {
            throw new UnauthorizedAccessException(
                "Authenticated user is required.");
        }

        string ownerUserId = authenticatedUserId.Trim();

        // Step 2: Validate request arguments.

        if (tripPlanId == Guid.Empty)
        {
            throw new ArgumentException(
                "Valid Trip Plan ID is required.",
                nameof(tripPlanId));
        }

        if (string.IsNullOrWhiteSpace(memberUserId))
        {
            throw new ArgumentException(
                "Member User ID is required.",
                nameof(memberUserId));
        }

        string targetUserId = memberUserId.Trim();

        // Step 3: Load trip from trusted backend storage.

        var trip = await _repository.GetTripPlanByIdAsync(
            tripPlanId,
            cancellationToken);

        if (trip is null)
        {
            throw new KeyNotFoundException(
                "Trip plan was not found.");
        }

        if (trip.Id != tripPlanId)
        {
            throw new InvalidOperationException(
                "Repository returned an unrelated trip.");
        }

        // Step 4: Authorize the trip owner.

        if (!string.Equals(
            trip.OwnerUserId,
            ownerUserId,
            StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException(
                "Only the trip owner can remove members.");
        }

        // The trip owner cannot remove themselves
        // using the membership revocation workflow.

        if (string.Equals(
            ownerUserId,
            targetUserId,
            StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The trip owner cannot remove themselves.");
        }

        // Step 5: Find the requested member.

        var member = await _repository.GetMemberAsync(
            tripPlanId,
            targetUserId,
            cancellationToken);

        if (member is null)
        {
            throw new KeyNotFoundException(
                "Trip member was not found.");
        }

        // Step 6: Check membership integrity.

        if (member.TripPlanId != tripPlanId ||
            !string.Equals(
                member.MemberUserId,
                targetUserId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Membership does not match the requested trip.");
        }

        if (member.Id == Guid.Empty)
        {
            throw new InvalidOperationException(
                "Stored membership ID is invalid.");
        }

        // Step 7: Repeated removal is idempotent.

        if (!member.IsActive)
        {
            return false;
        }

        // Step 8: Use trusted server time.

        DateTimeOffset removedAtUtc =
            _timeProvider.GetUtcNow().ToUniversalTime();

        if (removedAtUtc < member.JoinedAtUtc)
        {
            throw new InvalidOperationException(
                "Removal time cannot precede joining time.");
        }

        // Step 9: Atomically remove membership and
        // record a single notification event.
        //
        // Concurrent requests must not create
        // duplicate notifications.
        //
        // The repository owns the conditional
        // state transition and persistence.

        bool removed =
            await _repository.RemoveMemberAndQueueNotificationAsync(
                tripPlanId,
                member.Id,
                removedAtUtc,
                cancellationToken);

        // Never automatically restore GPS consent.
        // Removed members must be denied access by
        // subsequent private-trip and GPS authorization checks.

        return removed;
    }
}
