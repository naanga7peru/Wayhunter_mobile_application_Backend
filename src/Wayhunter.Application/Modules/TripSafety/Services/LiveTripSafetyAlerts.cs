using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.TravelPlanning.Interfaces;
using Wayhunter.Application.Modules.TripSafety.Interfaces;
using Wayhunter.Domain.Modules.TravelPlanning.Enums;

namespace Wayhunter.Application.Modules.TripSafety.Services;

public enum HazardAlertPhase { Detected, Active, Acknowledged, Resolved }
public enum SeparationChange { None, Separated, Recovered, InvalidLocation, StaleLocation }

public sealed record LiveTripAlertSettings(
    TimeSpan? HazardRepeatCooldown = null,
    double? SeparationMeters = null,
    double? RecoveryMeters = null,
    TimeSpan? MaximumLocationAge = null);

public sealed record HazardAlertUpdate(
    HazardWarningDto Warning, HazardAlertPhase Phase, bool ShouldWarn);

public sealed record SafetyLocation(
    Guid TripPlanId, Guid SessionId, string UserId,
    double Latitude, double Longitude, DateTimeOffset RecordedAtUtc);

// Process-local staging only. Retains alert state and timestamps, never positions.
public sealed class LiveTripAlertState
{
    internal const int Capacity = 4096;
    internal readonly object Gate = new();
    internal readonly Dictionary<(Guid Trip, Guid Session, string User, Guid Hazard), HazardEntry> Hazards = new();
    internal readonly Dictionary<(Guid Trip, Guid Session, string First, string Second), SeparationEntry> Pairs = new();

    internal sealed record HazardEntry(DateTimeOffset ExpiresAtUtc, HazardAlertPhase Phase, DateTimeOffset? LastWarning);
    internal sealed record SeparationEntry(DateTimeOffset FirstTime, DateTimeOffset SecondTime, bool Separated);
}

public sealed class LiveTripSafetyAlerts(
    TripHazardService hazards,
    ITripPlanRepository trips,
    ITripLiveLocationAccessReader access,
    LiveTripAlertState state,
    LiveTripAlertSettings settings,
    TimeProvider clock, IAuthoritativeTripHazardRouteReader? routes = null)
{
    public async Task<HazardAlertUpdate?> ObserveHazardAsync(
        Guid trip, Guid session, string user, Guid hazard, CancellationToken ct = default)
    {
        if (routes is null || settings.HazardRepeatCooldown is not { } cooldown || cooldown <= TimeSpan.Zero)
            return null;

        await RequireAccessAsync(trip, session, user, ct);
        var warnings = await hazards.GetWarningsForLiveTripAsync(trip, user, routes, ct);
        var warning = warnings.FirstOrDefault(item => item.HazardId == hazard);
        await RequireAccessAsync(trip, session, user, ct);

        lock (state.Gate)
        {
            ct.ThrowIfCancellationRequested();
            var now = clock.GetUtcNow();
            var key = (trip, session, user, hazard);
            RemoveExpiredHazards(now);
            state.Hazards.TryGetValue(key, out var entry);
            if (warning is null || warning.ExpiresAtUtc <= now)
                return null;

            if (entry is null)
            {
                if (state.Hazards.Count >= LiveTripAlertState.Capacity)
                    return null;

                entry = new(warning.ExpiresAtUtc, HazardAlertPhase.Detected, null);
            }

            var shouldWarn = entry.Phase == HazardAlertPhase.Detected ||
                (entry.Phase == HazardAlertPhase.Active && now - entry.LastWarning >= cooldown);
            if (shouldWarn)
                entry = entry with { Phase = HazardAlertPhase.Active, LastWarning = now };

            state.Hazards[key] = entry;
            return new(warning, entry.Phase, shouldWarn);
        }
    }

    public async Task<HazardAlertPhase> TransitionHazardAsync(
        Guid trip, Guid session, string user, Guid hazard, HazardAlertPhase target, CancellationToken ct = default)
    {
        await RequireAccessAsync(trip, session, user, ct);
        lock (state.Gate)
        {
            ct.ThrowIfCancellationRequested();
            RemoveExpiredHazards(clock.GetUtcNow());
            var key = (trip, session, user, hazard);
            if (!state.Hazards.TryGetValue(key, out var entry))
                throw new KeyNotFoundException("Active hazard alert was not found.");

            if (entry.Phase == target && target is HazardAlertPhase.Acknowledged or HazardAlertPhase.Resolved)
                return target;

            if ((entry.Phase, target) is not
                ((HazardAlertPhase.Active, HazardAlertPhase.Acknowledged) or
                 (HazardAlertPhase.Acknowledged, HazardAlertPhase.Resolved)))
                throw new InvalidOperationException("Invalid hazard alert transition.");

            state.Hazards[key] = entry with { Phase = target };
            return target;
        }
    }

    // Both samples come from trusted server-stamped live-location events.
    public async Task<SeparationChange> EvaluateSeparationAsync(
        Guid trip, Guid session, string user, SafetyLocation own, SafetyLocation peer, CancellationToken ct = default)
    {
        if (settings.SeparationMeters is not { } threshold || !double.IsFinite(threshold) || threshold <= 0 ||
            settings.RecoveryMeters is not { } recovery || !double.IsFinite(recovery) || recovery < 0 || recovery >= threshold ||
            settings.MaximumLocationAge is not { } maxAge || maxAge <= TimeSpan.Zero)
            return SeparationChange.None;

        if (own.TripPlanId != trip || peer.TripPlanId != trip ||
            own.SessionId != session || peer.SessionId != session ||
            !string.Equals(own.UserId, user, StringComparison.Ordinal) ||
            string.Equals(own.UserId, peer.UserId, StringComparison.Ordinal))
            throw new UnauthorizedAccessException("Location samples must belong to the same active trip session.");

        await RequireAccessAsync(trip, session, user, ct);
        await access.EnsureAllowedAsync(trip, session, peer.UserId, ct);

        if (!HazardRouteMatcher.IsValid(own.Latitude, own.Longitude) ||
            !HazardRouteMatcher.IsValid(peer.Latitude, peer.Longitude))
            return SeparationChange.InvalidLocation;

        var (first, second) = string.CompareOrdinal(own.UserId, peer.UserId) < 0 ? (own, peer) : (peer, own);
        var key = (trip, session, first.UserId, second.UserId);
        var distance = HazardRouteMatcher.DistanceBetweenMeters(own.Latitude, own.Longitude, peer.Latitude, peer.Longitude);

        lock (state.Gate)
        {
            ct.ThrowIfCancellationRequested();
            var now = clock.GetUtcNow();
            if (!IsFresh(first.RecordedAtUtc, now, maxAge) || !IsFresh(second.RecordedAtUtc, now, maxAge))
                return SeparationChange.StaleLocation;

            state.Pairs.TryGetValue(key, out var previous);
            if (previous is not null &&
                (first.RecordedAtUtc < previous.FirstTime || second.RecordedAtUtc < previous.SecondTime ||
                 (first.RecordedAtUtc == previous.FirstTime && second.RecordedAtUtc == previous.SecondTime)))
                return SeparationChange.None;

            if (previous is null && state.Pairs.Count >= LiveTripAlertState.Capacity)
                return SeparationChange.None;

            var wasSeparated = previous?.Separated == true;
            var separated = wasSeparated ? distance > recovery : distance >= threshold;
            state.Pairs[key] = new(first.RecordedAtUtc, second.RecordedAtUtc, separated);
            return (wasSeparated, separated) switch
            {
                (false, true) => SeparationChange.Separated,
                (true, false) => SeparationChange.Recovered,
                _ => SeparationChange.None
            };
        }
    }

    private async Task RequireAccessAsync(Guid trip, Guid session, string user, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(user) || user != user.Trim())
            throw new UnauthorizedAccessException("A trusted user identity is required.");

        await access.EnsureAllowedAsync(trip, session, user, ct);
        var plan = await trips.GetByIdAsync(trip, ct);
        if (plan is null || plan.DraftStatus == TripDraftStatus.Cancelled)
            throw new InvalidOperationException("Trip is not eligible for live safety alerts.");
    }

    private void RemoveExpiredHazards(DateTimeOffset now)
    {
        foreach (var key in state.Hazards.Where(item => item.Value.ExpiresAtUtc <= now)
                     .Select(item => item.Key).ToArray())
            state.Hazards.Remove(key);
    }

    private static bool IsFresh(DateTimeOffset time, DateTimeOffset now, TimeSpan maxAge) =>
        time != default && time <= now && now - time <= maxAge;
}