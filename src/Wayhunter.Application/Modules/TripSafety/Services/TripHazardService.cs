
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Wayhunter.Application.Modules.TravelPlanning.Interfaces;
using Wayhunter.Application.Modules.TripSafety.Interfaces;

using Wayhunter.Domain.Modules.TravelPlanning.Entities;
using Wayhunter.Domain.Modules.TravelPlanning.Enums;
using Wayhunter.Domain.Modules.TripSafety.Entities;
using Wayhunter.Domain.Modules.TripSafety.Enums;

namespace Wayhunter.Application.Modules.TripSafety.Services;

public sealed record CreateTripHazardRequest(
    double Latitude,
    double Longitude,
    string Description,
    string PhotoAssetReference,
    DateTimeOffset PhotoCapturedAtUtc);

public sealed record HazardWarningDto(
    Guid HazardId,
    HazardStatus Status,
    bool IsVerified,
    double? Latitude,
    double? Longitude,
    string? Description,
    bool HasPhotoEvidence,
    DateTimeOffset PhotoCapturedAtUtc,
    DateTimeOffset ReportedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    DateTimeOffset? VerifiedAtUtc,
    DateTimeOffset? ResolvedAtUtc)
{
    public static HazardWarningDto FromDomain(
        HazardReport hazard)
    {
        return new HazardWarningDto(
            hazard.Id,
            hazard.Status,
            hazard.Status == HazardStatus.Verified,
            hazard.Latitude,
            hazard.Longitude,
            hazard.Description,
            !string.IsNullOrWhiteSpace(
                hazard.PhotoAssetReference),
            hazard.PhotoCapturedAtUtc,
            hazard.ReportedAtUtc,
            hazard.ExpiresAtUtc,
            hazard.VerifiedAtUtc,
            hazard.ResolvedAtUtc);
    }
}

public sealed class TripHazardService
{
    private readonly IHazardReportRepository _hazards;
    private readonly ITripPlanRepository _trips;
    private readonly ITripMembershipRevocationRepository _members;
    private readonly IRoutesProvider _routes;
    private readonly TimeProvider _clock;

    public TripHazardService(
        IHazardReportRepository hazards,
        ITripPlanRepository trips,
        ITripMembershipRevocationRepository members,
        IRoutesProvider routes,
        TimeProvider clock)
    {
        _hazards = hazards ??
            throw new ArgumentNullException(nameof(hazards));

        _trips = trips ??
            throw new ArgumentNullException(nameof(trips));

        _members = members ??
            throw new ArgumentNullException(nameof(members));

        _routes = routes ??
            throw new ArgumentNullException(nameof(routes));

        _clock = clock ??
            throw new ArgumentNullException(nameof(clock));
    }

    private async Task<TripPlan> RequireTripAccessAsync(
        Guid tripPlanId,
        string? authenticatedUserId,
        CancellationToken cancellationToken)
    {
        if (tripPlanId == Guid.Empty)
        {
            throw new ArgumentException(
                "Valid Trip Plan ID is required.");
        }

        if (string.IsNullOrWhiteSpace(authenticatedUserId))
        {
            throw new UnauthorizedAccessException(
                "Authentication is required.");
        }

        var trip = await _trips.GetByIdAsync(
            tripPlanId,
            cancellationToken);

        if (trip is null)
        {
            throw new KeyNotFoundException(
                "Trip plan was not found.");
        }

        if (trip.DraftStatus == TripDraftStatus.Cancelled)
        {
            throw new InvalidOperationException(
                "Cancelled trips are not eligible for hazard alerts.");
        }

        var userId = authenticatedUserId.Trim();

        if (string.Equals(
            trip.OwnerUserId,
            userId,
            StringComparison.Ordinal))
        {
            return trip;
        }

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
                "User is not an active trip member.");
        }

        return trip;
    }

    // A community report always starts unverified.
    // No admin verification, GPS sharing or
    // public media access is created here.

    public async Task<HazardWarningDto> ReportAsync(
        Guid tripPlanId,
        string? authenticatedUserId,
        CreateTripHazardRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ArgumentNullException.ThrowIfNull(request);

        await RequireTripAccessAsync(
            tripPlanId,
            authenticatedUserId,
            cancellationToken);

        var now = _clock.GetUtcNow().ToUniversalTime();

        var hazard = HazardReport.ReportWithEvidence(
            Guid.NewGuid(),
            authenticatedUserId!.Trim(),
            now,
            request.PhotoCapturedAtUtc,
            request.Latitude,
            request.Longitude,
            request.Description,
            request.PhotoAssetReference);

        await _hazards.AddAsync(
            hazard,
            cancellationToken);

        return HazardWarningDto.FromDomain(hazard);
    }

    // Return route-matched warnings only.
    // The routing provider must return actual
    // planned-route geometry; no guessed geometry.

    public async Task<IReadOnlyList<HazardWarningDto>>
        GetWarningsForTripAsync(
            Guid tripPlanId,
            string? authenticatedUserId,
            CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var trip = await RequireTripAccessAsync(
            tripPlanId,
            authenticatedUserId,
            cancellationToken);

        var orderedStops = trip.Stops
            .OrderBy(x => x.OrderIndex)
            .ToList();

        if (orderedStops.Count < 2 ||
            orderedStops.Any(x =>
                !x.Latitude.HasValue ||
                !x.Longitude.HasValue))
        {
            return Array.Empty<HazardWarningDto>();
        }

        var waypoints = orderedStops
            .Select(x => new RouteWaypoint(
                x.Latitude!.Value,
                x.Longitude!.Value))
            .ToArray();

        var route = await _routes.CalculateRouteAsync(
            waypoints,
            cancellationToken);

        // Fail closed: provider unavailable or no route
        // geometry must never cause a global broadcast.
        if (!route.IsSuccess ||
            route.RouteGeometry is null ||
            route.RouteGeometry.Count < 2)
        {
            return Array.Empty<HazardWarningDto>();
        }

        return await MatchWarningsAsync(route.RouteGeometry, cancellationToken);
    }

    public async Task<IReadOnlyList<HazardWarningDto>> GetWarningsForLiveTripAsync(
        Guid tripPlanId, string authenticatedUserId, IAuthoritativeTripHazardRouteReader routes,
        CancellationToken cancellationToken)
    {
        await RequireTripAccessAsync(tripPlanId, authenticatedUserId, cancellationToken);
        var geometry = await routes.ReadAsync(tripPlanId, cancellationToken);
        return geometry is { Count: >= 2 }
            ? await MatchWarningsAsync(geometry, cancellationToken)
            : Array.Empty<HazardWarningDto>();
    }

    private async Task<IReadOnlyList<HazardWarningDto>> MatchWarningsAsync(
        IReadOnlyList<RouteWaypoint> geometry, CancellationToken cancellationToken)
    {
        var now = _clock.GetUtcNow().ToUniversalTime();

        var candidates = await _hazards.GetActiveAtAsync(
            now,
            cancellationToken);

        var warnings = candidates
            .Where(hazard =>
                hazard.Latitude.HasValue &&
                hazard.Longitude.HasValue &&
                hazard.IsWarningActive(now) &&
                HazardRouteMatcher.IsWithinOneKilometer(
                    geometry,
                    hazard.Latitude.Value,
                    hazard.Longitude.Value))
            .OrderByDescending(x => x.ReportedAtUtc)
            .Select(HazardWarningDto.FromDomain)
            .ToArray();

        return warnings;
    }

    // Caller must be authorized Admin by API layer.

    public async Task<HazardWarningDto> VerifyAsync(
        Guid hazardId,
        string authenticatedAdminId,
        CancellationToken cancellationToken = default)
    {
        var hazard = await GetRequiredHazardAsync(
            hazardId,
            cancellationToken);

        hazard.Verify(
            authenticatedAdminId,
            _clock.GetUtcNow());

        await _hazards.UpdateAsync(
            hazard,
            cancellationToken);

        return HazardWarningDto.FromDomain(hazard);
    }

    // Caller must be authorized Admin by API layer.

    public async Task<HazardWarningDto> ResolveAsync(
        Guid hazardId,
        string authenticatedAdminId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var hazard = await GetRequiredHazardAsync(
            hazardId,
            cancellationToken);

        hazard.Resolve(
            authenticatedAdminId,
            reason,
            _clock.GetUtcNow());

        await _hazards.UpdateAsync(
            hazard,
            cancellationToken);

        return HazardWarningDto.FromDomain(hazard);
    }

    // Called by the opt-in expiry worker.
    // Old warnings are already excluded from
    // GetWarningsForTripAsync before this job runs.

    public async Task<int> ExpireDueAsync(
        CancellationToken cancellationToken = default)
    {
        var now = _clock.GetUtcNow().ToUniversalTime();

        var due = await _hazards.GetDueForExpiryAsync(
            now,
            100,
            cancellationToken);

        var count = 0;

        foreach (var hazard in due)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (hazard.Status == HazardStatus.Resolved ||
                hazard.Status == HazardStatus.Expired)
            {
                continue;
            }

            hazard.Expire(now);

            await _hazards.UpdateAsync(
                hazard,
                cancellationToken);

            count++;
        }

        return count;
    }

    private async Task<HazardReport> GetRequiredHazardAsync(
        Guid hazardId,
        CancellationToken cancellationToken)
    {
        if (hazardId == Guid.Empty)
        {
            throw new ArgumentException(
                "Valid Hazard ID is required.");
        }

        return await _hazards.GetByIdAsync(
            hazardId,
            cancellationToken)
            ?? throw new KeyNotFoundException(
                "Hazard report was not found.");
    }
}
