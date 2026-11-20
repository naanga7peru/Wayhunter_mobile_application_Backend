
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Wayhunter.Application.Modules.TripSafety.Interfaces;
using Wayhunter.Domain.Modules.TravelPlanning.Enums;
using Wayhunter.Domain.Modules.TripSafety.Enums;

namespace Wayhunter.Application.Modules.TripSafety.Services;

public sealed class TripSafeCheckInService
{
    private readonly ITripMembershipRevocationRepository _members;
    private readonly ILiveTripSessionRepository _sessions;
    private readonly ITripSafetyCheckInStore _store;
    private readonly TimeProvider _clock;

    public TripSafeCheckInService(
        ITripMembershipRevocationRepository members,
        ILiveTripSessionRepository sessions,
        ITripSafetyCheckInStore store,
        TimeProvider clock)
    {
        _members = members
            ?? throw new ArgumentNullException(nameof(members));

        _sessions = sessions
            ?? throw new ArgumentNullException(nameof(sessions));

        _store = store
            ?? throw new ArgumentNullException(nameof(store));

        _clock = clock
            ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<TripSafeCheckInWriteResult> MarkSafeAsync(
        Guid tripPlanId,
        Guid sessionId,
        string? authenticatedUserId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (tripPlanId == Guid.Empty ||
            sessionId == Guid.Empty)
        {
            throw new ArgumentException(
                "Valid trip and session IDs are required.");
        }

        if (!Guid.TryParseExact(
                authenticatedUserId, "D", out var userGuid) ||
            userGuid == Guid.Empty)
        {
            throw new UnauthorizedAccessException(
                "Trusted user identity is required.");
        }

        var userId = userGuid.ToString("D");

        var trip = await _members.GetTripPlanByIdAsync(
            tripPlanId,
            cancellationToken);

        if (trip is null)
        {
            throw new KeyNotFoundException(
                "Trip plan was not found.");
        }

        var isOwner = string.Equals(
            trip.OwnerUserId,
            userId,
            StringComparison.Ordinal);

        if (!isOwner)
        {
            var member = await _members.GetMemberAsync(
                tripPlanId,
                userId,
                cancellationToken);

            if (member is null ||
                !member.IsActive ||
                member.TripPlanId != tripPlanId ||
                !string.Equals(
                    member.MemberUserId,
                    userId,
                    StringComparison.Ordinal))
            {
                throw new UnauthorizedAccessException(
                    "Only active trip members may mark themselves safe.");
            }
        }

        if (trip.DraftStatus == TripDraftStatus.Cancelled)
        {
            throw new InvalidOperationException(
                "Cancelled trips cannot receive check-ins.");
        }

        var session = await _sessions.GetByIdAsync(
            sessionId,
            cancellationToken);

        if (session is null ||
            session.TripPlanId != tripPlanId)
        {
            throw new KeyNotFoundException(
                "Trip session was not found.");
        }

        if (session.Status == LiveTripStatus.Finished)
        {
            throw new InvalidOperationException(
                "Finished trips cannot receive new check-ins.");
        }

        var checkIn = new TripSafeCheckIn(
            Guid.NewGuid(),
            tripPlanId,
            sessionId,
            userId,
            _clock.GetUtcNow().ToUniversalTime());

        // One logical check-in per user/session.
        // No location coordinates.
        // No automatic separation from membership.
        // No GPS consent state changes.

        return await _store.RecordAsync(
            checkIn,
            cancellationToken);
    }
}
