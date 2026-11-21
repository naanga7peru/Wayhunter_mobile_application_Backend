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
    DateTimeOffset ObservedAtUtc,
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
    // ObservedAtUtc is the UTC time of a fresh source observation made inside this call.
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
    // Technical staging bound per section. Product and persistence limits need approval.
    private const int ProjectionItemLimit = 256;

    public async Task<AdminSafetyResult> ReadAsync(
        string actor, AdminSafetyTarget target, CancellationToken ct, Func<Task<bool>>? revalidateAdmin = null)
    {
        if (!IsCanonicalUserId(actor))
            return new(AdminSafetyOutcome.Denied);

        if (audit is null)
            return new(AdminSafetyOutcome.Unavailable);

        AdminSafetyGrant? originalGrant = null;
        async Task<AdminSafetyResult> ReadAndAuthorizeAsync()
        {
            try
            {
                var result = await ReadAuthorizedAsync(actor, target, originalGrant,
                    grant => originalGrant ??= grant, ct);
                if (result.Outcome == AdminSafetyOutcome.Success &&
                    revalidateAdmin is not null && !await revalidateAdmin())
                    return new(AdminSafetyOutcome.Denied);

                return result;
            }
            catch (Exception exception) when (IsDependencyFailure(exception, ct))
            {
                return new(AdminSafetyOutcome.Unavailable);
            }
        }

        async Task<bool> RecordAsync(string action, AdminSafetyOutcome outcome)
        {
            try
            {
                await audit.RecordAsync(new(actor, action, target, clock.GetUtcNow(), outcome), ct);
                return true;
            }
            catch (Exception exception) when (IsDependencyFailure(exception, ct))
            {
                return false;
            }
        }

        var initial = await ReadAndAuthorizeAsync();
        if (!await RecordAsync("ReadTripSafety", initial.Outcome))
            return new(AdminSafetyOutcome.Unavailable);

        if (initial.Summary is null)
            return initial;

        // Audit storage can block. Validate again before releasing any data.
        var final = await ReadAndAuthorizeAsync();
        if (final.Summary is not null && !SameSummary(initial.Summary, final.Summary))
            final = new(AdminSafetyOutcome.Unavailable);

        if (final.Outcome != AdminSafetyOutcome.Success && !await RecordAsync("WithholdTripSafety", final.Outcome))
            return new(AdminSafetyOutcome.Unavailable);

        ct.ThrowIfCancellationRequested();
        return final;
    }

    private async Task<AdminSafetyResult> ReadAuthorizedAsync(string actor, AdminSafetyTarget target,
        AdminSafetyGrant? expectedGrant, Action<AdminSafetyGrant> captureGrant, CancellationToken ct)
    {
        if (target.TripPlanId == Guid.Empty || target.SessionId == Guid.Empty)
            return new(AdminSafetyOutcome.InvalidTarget);

        if (scope is null)
            return new(AdminSafetyOutcome.Unavailable);

        var grant = await scope.ReadGrantAsync(actor, target, ct);
        if (grant is null || grant.Target != target || !string.Equals(grant.Actor, actor, StringComparison.Ordinal))
            return new(AdminSafetyOutcome.Denied);

        if (expectedGrant is not null && grant != expectedGrant)
            return new(AdminSafetyOutcome.Denied);

        captureGrant(grant);
        var trip = await membership.GetTripPlanByIdAsync(target.TripPlanId, ct);
        var session = await sessions.GetByIdAsync(target.SessionId, ct);
        if (trip?.Id != target.TripPlanId || session?.SessionId != target.SessionId || session.TripPlanId != target.TripPlanId)
            return new(AdminSafetyOutcome.NotFound);

        var tripStatus = trip.DraftStatus;
        var owner = trip.OwnerUserId;
        var sessionStatus = session.Status;
        var started = session.StartedAtUtc;
        var finished = session.FinishedAtUtc;
        var active = tripStatus != TripDraftStatus.Cancelled && sessionStatus == LiveTripStatus.Active;
        var sections = grant.Sections & (AdminSafetySections.Participants | AdminSafetySections.Hazards |
            AdminSafetySections.Separation | AdminSafetySections.SafeCheckIn);
        if (!active)
            sections &= ~(AdminSafetySections.Hazards | AdminSafetySections.Separation);

        if (sections != AdminSafetySections.None && projections is null)
            return new(AdminSafetyOutcome.Unavailable);

        AdminSafetyProjection? data = null;
        if (sections != AdminSafetySections.None)
        {
            var readStarted = clock.GetUtcNow();
            data = await projections!.ReadAsync(grant with { Sections = sections }, ct);
            if (data is null || data.Target != target || data.ObservedAtUtc == default ||
                data.ObservedAtUtc.Offset != TimeSpan.Zero ||
                data.ObservedAtUtc < readStarted || data.ObservedAtUtc > clock.GetUtcNow())
                return new(AdminSafetyOutcome.Unavailable);
            data = CopyProjection(data, sections);
        }

        async Task<bool> IsParticipantAsync(string user)
        {
            if (string.Equals(owner, user, StringComparison.Ordinal))
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
                         checkIn.SessionId == target.SessionId && checkIn.CheckedInAtUtc >= started &&
                         checkIn.CheckedInAtUtc <= now &&
                         (!finished.HasValue || checkIn.CheckedInAtUtc <= finished.Value))
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

        var currentTrip = await membership.GetTripPlanByIdAsync(target.TripPlanId, ct);
        var currentSession = await sessions.GetByIdAsync(target.SessionId, ct);
        if (currentTrip?.Id != target.TripPlanId || currentTrip.DraftStatus != tripStatus || currentTrip.OwnerUserId != owner ||
            currentSession?.SessionId != target.SessionId || currentSession.TripPlanId != target.TripPlanId ||
            currentSession.Status != sessionStatus || currentSession.StartedAtUtc != started || currentSession.FinishedAtUtc != finished)
            return new(AdminSafetyOutcome.Unavailable);

        // Recheck the exact grant before returning a previously read projection.
        var currentGrant = await scope.ReadGrantAsync(actor, target, ct);
        if (currentGrant != grant)
            return new(AdminSafetyOutcome.Denied);

        return new(AdminSafetyOutcome.Success, new(target, tripStatus, sessionStatus,
            participantCount, hazards, separationCount, checkInCount));
    }

    private static AdminSafetyProjection CopyProjection(AdminSafetyProjection data, AdminSafetySections sections) =>
        data with
        {
            Participants = sections.HasFlag(AdminSafetySections.Participants)
                ? CopyValidated(data.Participants, IsCanonicalUserId) : null,
            Hazards = sections.HasFlag(AdminSafetySections.Hazards) ? CopyValidated(data.Hazards, hazard =>
                hazard is not null && Enum.IsDefined(hazard.Status) &&
                hazard.PhotoCapturedAtUtc != default && hazard.PhotoCapturedAtUtc <= data.ObservedAtUtc &&
                hazard.ExpiresAtUtc > hazard.PhotoCapturedAtUtc) : null,
            ActiveSeparations = sections.HasFlag(AdminSafetySections.Separation) ? CopyValidated(data.ActiveSeparations, pair =>
                pair is not null && IsCanonicalUserId(pair.FirstUserId) && IsCanonicalUserId(pair.SecondUserId)) : null,
            CheckIns = sections.HasFlag(AdminSafetySections.SafeCheckIn) ? CopyValidated(data.CheckIns, item =>
                item is not null && item.CheckInId != Guid.Empty && item.TripPlanId != Guid.Empty &&
                item.SessionId != Guid.Empty && IsCanonicalUserId(item.UserId) &&
                item.CheckedInAtUtc != default && item.CheckedInAtUtc <= data.ObservedAtUtc) : null
        };

    private static T[]? CopyValidated<T>(IReadOnlyList<T>? items, Func<T, bool> validate)
    {
        if (items is null)
            return null;

        var count = items.Count;
        if (count is < 0 or > ProjectionItemLimit)
            throw new InvalidOperationException("Safety projection exceeds its section limit.");

        var copy = items.Take(ProjectionItemLimit + 1).ToArray();
        if (copy.Length != count || !copy.All(validate))
            throw new InvalidOperationException("Safety projection contains invalid entries.");

        return copy;
    }

    private static bool IsCanonicalUserId(string user) =>
        Guid.TryParseExact(user, "D", out var id) && id != Guid.Empty && user == id.ToString("D");

    private static bool SameSummary(AdminSafetySummary initial, AdminSafetySummary final) =>
        (initial with { Hazards = null }) == (final with { Hazards = null }) &&
        (initial.Hazards is null ? final.Hazards is null :
            final.Hazards is not null && initial.Hazards.SequenceEqual(final.Hazards));

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
