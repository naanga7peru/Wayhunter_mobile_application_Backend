using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.TripSafety.Interfaces;
using Wayhunter.Domain.Modules.TravelPlanning.Enums;
using Wayhunter.Domain.Modules.TripSafety.Enums;

namespace Wayhunter.Application.Modules.TripSafety.Services;

[Flags]
public enum AdminSafetySections { None = 0, Participants = 1, Hazards = 2, Separation = 4, SafeCheckIn = 8 }

public sealed record AdminSafetyTarget(Guid TripPlanId, Guid SessionId);
public sealed record AdminSafetyGrant(AdminSafetyTarget Target, string Actor, AdminSafetySections Sections);
public sealed record AdminSafetyHazard(HazardStatus Status, DateTimeOffset PhotoCapturedAtUtc, DateTimeOffset ExpiresAtUtc);
public sealed record AdminSafetyPair(string FirstUserId, string SecondUserId);

// Projections contain no positions, contact information, evidence references or tokens.
// Null means the section is unavailable or outside the supplied grant.
public sealed record AdminSafetyProjection(
    AdminSafetyTarget Target,
    IReadOnlyList<string>? Participants = null,
    IReadOnlyList<AdminSafetyHazard>? Hazards = null,
    IReadOnlyList<AdminSafetyPair>? ActiveSeparations = null,
    IReadOnlyList<TripSafeCheckIn>? CheckIns = null);

public interface IAdminSafetyScopeReader
{
    // Null means access denied. Unavailable authorization data must fail closed.
    Task<AdminSafetyGrant?> ReadGrantAsync(string actor, AdminSafetyTarget target, CancellationToken ct);
}

public interface IAdminSafetyProjectionReader
{
    // Read only granted sections from authoritative, exact-trip/session sources.
    Task<AdminSafetyProjection?> ReadAsync(AdminSafetyGrant grant, CancellationToken ct);
}

public enum AdminSafetyOutcome { Success, InvalidTarget, Denied, NotFound, Unavailable }

public sealed record AdminSafetyAuditRecord(
    string Actor, string Action, AdminSafetyTarget Target, DateTimeOffset TimestampUtc, AdminSafetyOutcome Outcome);

public interface IAdminSafetyAuditSink
{
    Task RecordAsync(AdminSafetyAuditRecord record, CancellationToken ct);
}

public sealed record AdminSafetySummary(
    AdminSafetyTarget Target, TripDraftStatus TripStatus, LiveTripStatus SessionStatus,
    int? ActiveParticipantCount, IReadOnlyList<AdminSafetyHazard>? Hazards,
    int? ActiveSeparationCount, int? CheckedInParticipantCount);

public sealed record AdminSafetyResult(AdminSafetyOutcome Outcome, AdminSafetySummary? Summary = null);

public sealed class AdminSafetyMonitoringService(
    ITripMembershipRevocationRepository membership,
    ILiveTripSessionRepository sessions,
    ITripLiveLocationAccessReader liveAccess,
    TimeProvider clock,
    IAdminSafetyScopeReader? scope = null,
    IAdminSafetyProjectionReader? projections = null,
    IAdminSafetyAuditSink? audit = null)
{
    public async Task<AdminSafetyResult> ReadAsync(string actor, AdminSafetyTarget target, CancellationToken ct)
    {
        if (!Guid.TryParseExact(actor, "D", out var actorId) || actorId == Guid.Empty)
            return new(AdminSafetyOutcome.Denied);

        if (audit is null)
            return new(AdminSafetyOutcome.Unavailable);

        AdminSafetyResult result;
        try
        {
            result = await ReadAuthorizedAsync(actor, target, ct);
        }
        catch (Exception exception) when (IsDependencyFailure(exception, ct))
        {
            result = new(AdminSafetyOutcome.Unavailable);
        }

        try
        {
            await audit.RecordAsync(new(actor, "ReadTripSafety", target, clock.GetUtcNow(), result.Outcome), ct);
        }
        catch (Exception exception) when (IsDependencyFailure(exception, ct))
        {
            return new(AdminSafetyOutcome.Unavailable);
        }

        return result;
    }

    private async Task<AdminSafetyResult> ReadAuthorizedAsync(string actor, AdminSafetyTarget target, CancellationToken ct)
    {
        if (target.TripPlanId == Guid.Empty || target.SessionId == Guid.Empty)
            return new(AdminSafetyOutcome.InvalidTarget);

        if (scope is null)
            return new(AdminSafetyOutcome.Unavailable);

        var grant = await scope.ReadGrantAsync(actor, target, ct);
        if (grant is null || grant.Target != target || !string.Equals(grant.Actor, actor, StringComparison.Ordinal))
            return new(AdminSafetyOutcome.Denied);

        var trip = await membership.GetTripPlanByIdAsync(target.TripPlanId, ct);
        var session = await sessions.GetByIdAsync(target.SessionId, ct);
        if (trip?.Id != target.TripPlanId || session?.SessionId != target.SessionId || session.TripPlanId != target.TripPlanId)
            return new(AdminSafetyOutcome.NotFound);

        var active = trip.DraftStatus != TripDraftStatus.Cancelled && session.Status == LiveTripStatus.Active;
        var sections = grant.Sections & (AdminSafetySections.Participants | AdminSafetySections.Hazards |
            AdminSafetySections.Separation | AdminSafetySections.SafeCheckIn);
        if (!active)
            sections &= ~(AdminSafetySections.Hazards | AdminSafetySections.Separation);

        if (sections != AdminSafetySections.None && projections is null)
            return new(AdminSafetyOutcome.Unavailable);

        var data = sections == AdminSafetySections.None
            ? null : await projections!.ReadAsync(grant with { Sections = sections }, ct);
        if (data is not null && data.Target != target)
            return new(AdminSafetyOutcome.Unavailable);

        async Task<bool> IsParticipantAsync(string user)
        {
            if (!Guid.TryParseExact(user, "D", out var id) || id == Guid.Empty || user != id.ToString("D"))
                return false;

            if (string.Equals(trip.OwnerUserId, user, StringComparison.Ordinal))
                return true;

            var member = await membership.GetMemberAsync(target.TripPlanId, user, ct);
            return member is { IsActive: true } && member.TripPlanId == target.TripPlanId &&
                string.Equals(member.MemberUserId, user, StringComparison.Ordinal);
        }

        int? participantCount = null;
        if (sections.HasFlag(AdminSafetySections.Participants) && data?.Participants is { } participants)
        {
            participantCount = 0;
            foreach (var user in participants.Distinct(StringComparer.Ordinal))
                if (await IsParticipantAsync(user))
                    participantCount++;
        }

        IReadOnlyList<AdminSafetyHazard>? hazards = null;
        if (sections.HasFlag(AdminSafetySections.Hazards) && data?.Hazards is { } matchedHazards)
        {
            var now = clock.GetUtcNow();
            hazards = matchedHazards.Where(hazard => hazard.Status is HazardStatus.Reported or HazardStatus.Verified &&
                hazard.PhotoCapturedAtUtc != default && hazard.PhotoCapturedAtUtc <= now &&
                hazard.ExpiresAtUtc > now).ToArray();
        }

        int? checkInCount = null;
        if (sections.HasFlag(AdminSafetySections.SafeCheckIn) && data?.CheckIns is { } checkIns)
        {
            checkInCount = 0;
            var now = clock.GetUtcNow();
            foreach (var user in checkIns.Where(checkIn => checkIn.TripPlanId == target.TripPlanId &&
                         checkIn.SessionId == target.SessionId && checkIn.CheckedInAtUtc >= session.StartedAtUtc &&
                         checkIn.CheckedInAtUtc <= now &&
                         (!session.FinishedAtUtc.HasValue || checkIn.CheckedInAtUtc <= session.FinishedAtUtc.Value))
                         .Select(checkIn => checkIn.UserId).Distinct(StringComparer.Ordinal))
                if (await IsParticipantAsync(user))
                    checkInCount++;
        }

        int? separationCount = null;
        if (sections.HasFlag(AdminSafetySections.Separation) && data?.ActiveSeparations is { } pairs)
        {
            var eligiblePairs = new HashSet<(string First, string Second)>();
            foreach (var pair in pairs)
            {
                var order = string.CompareOrdinal(pair.FirstUserId, pair.SecondUserId);
                if (order == 0)
                    continue;

                var key = order < 0 ? (pair.FirstUserId, pair.SecondUserId) : (pair.SecondUserId, pair.FirstUserId);
                if (!eligiblePairs.Contains(key) && await IsParticipantAsync(key.Item1) && await IsParticipantAsync(key.Item2) &&
                    await HasLiveAccessAsync(target, key.Item1, ct) && await HasLiveAccessAsync(target, key.Item2, ct))
                    eligiblePairs.Add(key);
            }

            separationCount = eligiblePairs.Count;
        }

        // Recheck the exact grant before returning a previously read projection.
        var currentGrant = await scope.ReadGrantAsync(actor, target, ct);
        if (currentGrant != grant)
            return new(AdminSafetyOutcome.Denied);

        return new(AdminSafetyOutcome.Success, new(target, trip.DraftStatus, session.Status,
            participantCount, hazards, separationCount, checkInCount));
    }

    private async Task<bool> HasLiveAccessAsync(AdminSafetyTarget target, string user, CancellationToken ct)
    {
        try
        {
            await liveAccess.EnsureAllowedAsync(target.TripPlanId, target.SessionId, user, ct);
            return true;
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or InvalidOperationException or KeyNotFoundException)
        {
            return false;
        }
    }

    private static bool IsDependencyFailure(Exception exception, CancellationToken ct) =>
        exception is not OutOfMemoryException && !ct.IsCancellationRequested;
}
