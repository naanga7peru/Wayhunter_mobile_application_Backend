using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.TripSafety.Dtos;
using Wayhunter.Application.Modules.TripSafety.Interfaces;
using Wayhunter.Application.Modules.TripSafety.Validators;
using Wayhunter.Domain.Modules.TripSafety.Entities;
using Wayhunter.Domain.Modules.TripSafety.Enums;

namespace Wayhunter.Infrastructure.Persistence.Repositories.TripSafety;

public sealed class InMemoryTripSharingConsentRepository
    : ITripSharingConsentRepository,
      ITripLiveLocationAccessReader
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

    // Development/test-only setup.
    // Must not be exposed through an HTTP endpoint.

    public void SeedSessionForTests(
        LiveTripSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        lock (_sync)
        {
            if (!_sessions.TryAdd(
                session.SessionId, session))
            {
                throw new InvalidOperationException(
                    "Session already exists.");
            }
        }
    }

    public void SeedAcceptedMemberForTests(
        TripMember member)
    {
        ArgumentNullException.ThrowIfNull(member);

        if (!member.IsActive)
        {
            throw new InvalidOperationException(
                "An active accepted member is required.");
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

    // Test simulation of membership revocation.
    // Production must integrate the canonical T04 flow.

    public void RevokeMembershipForTests(
        Guid tripPlanId,
        string memberUserId,
        DateTimeOffset serverTimeUtc)
    {
        if (string.IsNullOrWhiteSpace(memberUserId))
            throw new ArgumentException(
                "Member User ID is required.",
                nameof(memberUserId));

        if (serverTimeUtc == default)
            throw new ArgumentException(
                "Valid server time is required.",
                nameof(serverTimeUtc));

        var userId = memberUserId.Trim();
        var time = serverTimeUtc.ToUniversalTime();

        lock (_sync)
        {
            var key = (tripPlanId, userId);

            if (!_members.ContainsKey(key))
                throw new KeyNotFoundException(
                    "Member was not found.");

            var affected = _sessions.Values
                .Where(x => x.TripPlanId == tripPlanId)
                .Select(x => x.SessionId)
                .ToArray();

            // Validate before changing stored state.
            foreach (var sessionId in affected)
            {
                if (_consents.TryGetValue(
                        (sessionId, userId),
                        out var consent) &&
                    consent.IsGranted &&
                    consent.GrantedAtUtc.HasValue &&
                    time < consent.GrantedAtUtc.Value)
                {
                    throw new InvalidOperationException(
                        "Revocation cannot precede consent grant.");
                }
            }

            _revoked.Add(key);

            foreach (var sessionId in affected)
            {
                if (_consents.TryGetValue(
                        (sessionId, userId),
                        out var consent) &&
                    consent.IsGranted)
                {
                    consent.Revoke(time);
                }
            }
        }
    }

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

    // T01: Explicit consent changes only.

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

        var userId = authenticatedUserId.Trim();
        var memberKey = (tripPlanId, userId);
        var consentKey = (sessionId, userId);
        var now = serverTimeUtc.ToUniversalTime();

        lock (_sync)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!_sessions.TryGetValue(
                sessionId, out var session))
            {
                throw new KeyNotFoundException(
                    "Live trip session was not found.");
            }

            if (session.TripPlanId != tripPlanId)
            {
                throw new UnauthorizedAccessException(
                    "Session belongs to another trip.");
            }

            if (session.Status == LiveTripStatus.Finished)
            {
                throw new InvalidOperationException(
                    "Finished trips cannot change GPS consent.");
            }

            if (!_members.TryGetValue(
                    memberKey, out var member) ||
                !member.IsActive ||
                member.TripPlanId != tripPlanId ||
                !string.Equals(
                    member.MemberUserId,
                    userId,
                    StringComparison.Ordinal) ||
                _revoked.Contains(memberKey))
            {
                throw new UnauthorizedAccessException(
                    "Active accepted membership is required.");
            }

            if (now < member.JoinedAtUtc ||
                now < session.StartedAtUtc)
            {
                throw new InvalidOperationException(
                    "Consent time cannot precede joining or trip start.");
            }

            // Default GPS permission is always disabled.
            if (!_consents.TryGetValue(
                consentKey, out var consent))
            {
                consent = TripGpsConsent.CreateDisabled(
                    sessionId, userId);

                _consents.Add(consentKey, consent);
            }

            if (grant)
                consent.Grant(now);
            else
                consent.Revoke(now);

            return Task.FromResult(
                GpsConsentDto.FromDomain(consent));
        }
    }

    // T02: Called before subscribing, publishing,
    // and receiving precise GPS updates.

    public Task EnsureAllowedAsync(
        Guid tripPlanId,
        Guid sessionId,
        string authenticatedUserId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (tripPlanId == Guid.Empty ||
            sessionId == Guid.Empty)
        {
            throw new ArgumentException(
                "Valid trip and session IDs are required.");
        }

        if (string.IsNullOrWhiteSpace(
            authenticatedUserId))
        {
            throw new UnauthorizedAccessException(
                "Authentication is required.");
        }

        var userId = authenticatedUserId.Trim();

        lock (_sync)
        {
            cancellationToken.ThrowIfCancellationRequested();

            _sessions.TryGetValue(
                sessionId, out var session);

            _members.TryGetValue(
                (tripPlanId, userId),
                out var member);

            _consents.TryGetValue(
                (sessionId, userId),
                out var consent);

            bool authorized =
                member is not null &&
                member.IsActive &&
                member.TripPlanId == tripPlanId &&
                string.Equals(
                    member.MemberUserId,
                    userId,
                    StringComparison.Ordinal) &&
                !_revoked.Contains((tripPlanId, userId));

            // Requires:
            // - Active session
            // - Matching trip
            // - Active membership
            // - Explicit, unrevoked GPS consent
            // - Matching session and identity

            GroupLiveLocationPublishValidator
                .EnsureCanPublish(
                    session,
                    consent,
                    tripPlanId,
                    userId,
                    authorized);
        }

        return Task.CompletedTask;
    }
}
