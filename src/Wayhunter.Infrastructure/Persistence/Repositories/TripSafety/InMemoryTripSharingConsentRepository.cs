
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.TripSafety.Dtos;
using Wayhunter.Application.Modules.TripSafety.Interfaces;
using Wayhunter.Domain.Modules.TripSafety.Entities;
using Wayhunter.Domain.Modules.TripSafety.Enums;

namespace Wayhunter.Infrastructure.Persistence.Repositories.TripSafety;

// Development and unit-test repository only.
//
// Production integration must use the canonical
// trip session and membership persistence.
// Do not expose test seed methods through HTTP.

public sealed class InMemoryTripSharingConsentRepository
    : ITripSharingConsentRepository
{
    private readonly object _sync = new();

    private readonly Dictionary<Guid, LiveTripSession>
        _sessions = new();

    private readonly Dictionary<
        (Guid TripId, string UserId), TripMember>
        _members = new();

    private readonly HashSet<
        (Guid TripId, string UserId)>
        _revoked = new();

    private readonly Dictionary<
        (Guid SessionId, string UserId), TripGpsConsent>
        _consents = new();

    // Test-only session setup.
    public void SeedSessionForTests(
        LiveTripSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        lock (_sync)
        {
            if (!_sessions.TryAdd(
                session.SessionId,
                session))
            {
                throw new InvalidOperationException(
                    "Session already exists.");
            }
        }
    }

    // Test-only accepted member setup.
    public void SeedAcceptedMemberForTests(
        TripMember member)
    {
        ArgumentNullException.ThrowIfNull(member);

        if (!member.IsActive)
        {
            throw new InvalidOperationException(
                "Only an active accepted member may be seeded.");
        }

        lock (_sync)
        {
            var key = (
                member.TripPlanId,
                member.MemberUserId);

            if (!_members.TryAdd(key, member))
            {
                throw new InvalidOperationException(
                    "Membership already exists.");
            }
        }
    }

    // Simulates the M3-MS03-T04 membership removal.
    // Production removal must be coordinated with
    // canonical membership and consent persistence.
    public void RevokeMembershipForTests(
        Guid tripPlanId,
        string memberUserId,
        DateTimeOffset serverTimeUtc)
    {
        if (string.IsNullOrWhiteSpace(memberUserId))
        {
            throw new ArgumentException(
                "Member User ID is required.",
                nameof(memberUserId));
        }

        if (serverTimeUtc == default)
        {
            throw new ArgumentException(
                "Valid server time is required.",
                nameof(serverTimeUtc));
        }

        string userId = memberUserId.Trim();
        var time = serverTimeUtc.ToUniversalTime();

        lock (_sync)
        {
            var key = (tripPlanId, userId);

            if (!_members.ContainsKey(key))
            {
                throw new KeyNotFoundException(
                    "Member was not found.");
            }

            // Validate all consent transitions
            // before changing membership state.

            foreach (var session in _sessions.Values
                .Where(x => x.TripPlanId == tripPlanId))
            {
                var consentKey = (
                    session.SessionId,
                    userId);

                if (_consents.TryGetValue(
                    consentKey,
                    out var existing) &&
                    existing.IsGranted &&
                    existing.GrantedAtUtc.HasValue &&
                    time < existing.GrantedAtUtc.Value)
                {
                    throw new InvalidOperationException(
                        "Revocation cannot precede consent grant.");
                }
            }

            // Prevent future consent changes.
            _revoked.Add(key);

            // Revoke previously granted consent.
            foreach (var session in _sessions.Values
                .Where(x => x.TripPlanId == tripPlanId))
            {
                var consentKey = (
                    session.SessionId,
                    userId);

                if (_consents.TryGetValue(
                    consentKey,
                    out var consent) &&
                    consent.IsGranted)
                {
                    consent.Revoke(time);
                }
            }
        }
    }

    // Test-only snapshot reader.
    public GpsConsentDto? GetSnapshotForTests(
        Guid sessionId,
        string userId)
    {
        lock (_sync)
        {
            return _consents.TryGetValue(
                (sessionId, userId),
                out var consent)
                    ? GpsConsentDto.FromDomain(consent)
                    : null;
        }
    }

    public Task<GpsConsentDto> SetMyConsentAsync(
        Guid tripPlanId,
        Guid sessionId,
        string authenticatedUserId,
        bool grant,
        DateTimeOffset serverTimeUtc,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (tripPlanId == Guid.Empty ||
            sessionId == Guid.Empty)
        {
            throw new ArgumentException(
                "A valid trip and session are required.");
        }

        if (string.IsNullOrWhiteSpace(
            authenticatedUserId))
        {
            throw new UnauthorizedAccessException(
                "Authentication is required.");
        }

        if (serverTimeUtc == default)
        {
            throw new ArgumentException(
                "Valid UTC server time is required.",
                nameof(serverTimeUtc));
        }

        string userId = authenticatedUserId.Trim();

        var memberKey = (
            tripPlanId,
            userId);

        var consentKey = (
            sessionId,
            userId);

        var now = serverTimeUtc.ToUniversalTime();

        // Authorization and consent transition happen
        // under the same lock.
        //
        // This provides in-memory atomicity only.

        lock (_sync)
        {
            // 1. Session must exist.
            if (!_sessions.TryGetValue(
                sessionId,
                out var session))
            {
                throw new KeyNotFoundException(
                    "Live trip session was not found.");
            }

            // 2. Session must belong to this trip.
            if (session.TripPlanId != tripPlanId)
            {
                throw new UnauthorizedAccessException(
                    "Session belongs to another trip.");
            }

            // 3. Finished sessions cannot change consent.
            if (session.Status == LiveTripStatus.Finished)
            {
                throw new InvalidOperationException(
                    "Finished trips cannot change GPS consent.");
            }

            // 4. Only an active accepted member
            // may change their own consent.

            if (!_members.TryGetValue(
                    memberKey,
                    out var member) ||
                !member.IsActive ||
                member.TripPlanId != tripPlanId ||
                !string.Equals(
                    member.MemberUserId,
                    userId,
                    StringComparison.Ordinal) ||
                _revoked.Contains(memberKey))
            {
                throw new UnauthorizedAccessException(
                    "An active accepted trip membership is required.");
            }

            // 5. Validate server timestamps.
            if (now < member.JoinedAtUtc ||
                now < session.StartedAtUtc)
            {
                throw new InvalidOperationException(
                    "Consent time cannot precede joining or session start.");
            }

            // 6. Default consent is always OFF.
            //
            // Accepting an invitation never creates
            // or enables GPS sharing automatically.

            if (!_consents.TryGetValue(
                consentKey,
                out var consent))
            {
                consent = TripGpsConsent.CreateDisabled(
                    sessionId,
                    userId);

                _consents.Add(consentKey, consent);
            }

            // 7. Explicit Grant or Revoke.
            if (grant)
            {
                consent.Grant(now);
            }
            else
            {
                consent.Revoke(now);
            }

            // 8. Return immutable response.
            return Task.FromResult(
                GpsConsentDto.FromDomain(consent));
        }
    }
}
