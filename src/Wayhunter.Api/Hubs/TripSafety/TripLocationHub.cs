
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

using Wayhunter.Api.Contracts;
using Wayhunter.Application.Modules.TripSafety.Interfaces;

namespace Wayhunter.Api.Hubs.TripSafety;

[Authorize]
public sealed class TripLocationHub : Hub
{
    private readonly ITripLiveLocationAccessReader _access;
    private readonly TripLocationSubscriberRegistry _registry;
    private readonly TripLocationFanoutService _fanout;
    private readonly TimeProvider _clock;

    public TripLocationHub(
        ITripLiveLocationAccessReader access,
        TripLocationSubscriberRegistry registry,
        TripLocationFanoutService fanout,
        TimeProvider clock)
    {
        _access = access
            ?? throw new ArgumentNullException(nameof(access));

        _registry = registry
            ?? throw new ArgumentNullException(nameof(registry));

        _fanout = fanout
            ?? throw new ArgumentNullException(nameof(fanout));

        _clock = clock
            ?? throw new ArgumentNullException(nameof(clock));
    }

    // ==============================================
    // M1-M3 TRUSTED IDENTITY VALIDATION
    // ==============================================

    private string AuthenticatedUserId()
    {
        var principal = Context.User;

        // Reject anonymous, malformed, duplicate
        // and conflicting identity claims.
        //
        // M1 owns authentication and trusted identity.
        // M3 owns trip/session/GPS authorization.

        if (principal is null ||
            !TrustedIdentityAccessContract.TryResolveUserId(
                principal,
                out var userId))
        {
            throw new HubException(
                "Trusted user identity is required.");
        }

        return userId;
    }

    private static string GroupName(
        Guid tripPlanId,
        Guid sessionId)
    {
        return $"trip:{tripPlanId:N}:session:{sessionId:N}";
    }

    // ==============================================
    // LIVE GPS ACCESS AUTHORIZATION
    // ==============================================

    private async Task RequireAccessAsync(
        Guid tripPlanId,
        Guid sessionId,
        string userId)
    {
        try
        {
            await _access.EnsureAllowedAsync(
                tripPlanId,
                sessionId,
                userId,
                Context.ConnectionAborted);
        }
        catch (UnauthorizedAccessException)
        {
            throw new HubException(
                "Trip location access denied.");
        }
        catch (InvalidOperationException)
        {
            throw new HubException(
                "Trip location is not available.");
        }
        catch (KeyNotFoundException)
        {
            throw new HubException(
                "Trip location is not available.");
        }
    }

    // ==============================================
    // JOIN TRIP
    // ==============================================

    // Client invokes:
    // JoinTrip(tripPlanId, sessionId)
    //
    // Authentication alone never authorizes GPS.
    // Membership, session state and explicit consent
    // are verified before joining.

    [HubMethodName("JoinTrip")]
    public async Task JoinTripAsync(
        Guid tripPlanId,
        Guid sessionId)
    {
        var userId = AuthenticatedUserId();

        await RequireAccessAsync(
            tripPlanId,
            sessionId,
            userId);

        var existing = _registry.Find(
            Context.ConnectionId);

        if (existing is not null)
        {
            if (!string.Equals(
                existing.UserId,
                userId,
                StringComparison.Ordinal))
            {
                throw new HubException(
                    "Connection identity mismatch.");
            }

            // A connection may only belong to
            // one active trip/session subscription.

            if (existing.TripPlanId != tripPlanId ||
                existing.SessionId != sessionId)
            {
                await Groups.RemoveFromGroupAsync(
                    Context.ConnectionId,
                    GroupName(
                        existing.TripPlanId,
                        existing.SessionId),
                    Context.ConnectionAborted);
            }
        }

        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            GroupName(
                tripPlanId,
                sessionId),
            Context.ConnectionAborted);

        _registry.Register(
            Context.ConnectionId,
            tripPlanId,
            sessionId,
            userId);
    }

    // ==============================================
    // PUBLISH LIVE GPS
    // ==============================================

    // Client invokes:
    // PublishLocation(tripPlanId, sessionId, request)

    [HubMethodName("PublishLocation")]
    public async Task PublishLocationAsync(
        Guid tripPlanId,
        Guid sessionId,
        PublishTripLocationRequest request)
    {
        var userId = AuthenticatedUserId();

        var connection = _registry.Find(
            Context.ConnectionId);

        // Prevent publishing from connections
        // that have not joined this exact
        // trip and live session.

        if (connection is null ||
            connection.TripPlanId != tripPlanId ||
            connection.SessionId != sessionId ||
            !string.Equals(
                connection.UserId,
                userId,
                StringComparison.Ordinal))
        {
            throw new HubException(
                "Join this trip session before publishing.");
        }

        // Revalidate publisher for every update.
        //
        // Revoked members and users without GPS
        // consent cannot continue publishing.

        await RequireAccessAsync(
            tripPlanId,
            sessionId,
            userId);

        TripLocationEventDto location;

        try
        {
            location =
                TripLocationInputValidator.CreateEvent(
                    tripPlanId,
                    sessionId,
                    userId,
                    request,
                    _clock.GetUtcNow());
        }
        catch (ArgumentException)
        {
            throw new HubException(
                "Invalid location event.");
        }

        // ==========================================
        // GPS PRIVACY: AUTHORIZED RECEIVERS ONLY
        // ==========================================

        // Never use Clients.Group() to broadcast
        // precise GPS without per-user checks.
        //
        // A user may have revoked consent or lost
        // membership after originally joining.

        var recipients =
            await _fanout.GetAuthorizedRecipientsAsync(
                tripPlanId,
                sessionId,
                Context.ConnectionAborted);

        foreach (var recipient in recipients)
        {
            // Fanout can await storage after the initial publisher check.
            await RequireAccessAsync(tripPlanId, sessionId, userId);

            // Recheck recipient immediately before
            // delivering a GPS event.

            try
            {
                await _access.EnsureAllowedAsync(
                    tripPlanId,
                    sessionId,
                    recipient.UserId,
                    Context.ConnectionAborted);
            }
            catch (UnauthorizedAccessException)
            {
                _registry.RemoveIfUnchanged(recipient);
                continue;
            }
            catch (InvalidOperationException)
            {
                _registry.RemoveIfUnchanged(recipient);
                continue;
            }
            catch (KeyNotFoundException)
            {
                _registry.RemoveIfUnchanged(recipient);
                continue;
            }

            // Protect against stale connections
            // and connection reassignment.

            if (_registry.Find(Context.ConnectionId) != connection ||
                _registry.Find(recipient.ConnectionId) != recipient)
            {
                continue;
            }

            await Clients.Client(
                recipient.ConnectionId).SendAsync(
                    "TripLocationUpdated",
                    location,
                    Context.ConnectionAborted);
        }
    }

    // ==============================================
    // LEAVE TRIP
    // ==============================================

    [HubMethodName("LeaveTrip")]
    public async Task LeaveTripAsync()
    {
        var userId = AuthenticatedUserId();

        var existing = _registry.Find(
            Context.ConnectionId);

        if (existing is null)
        {
            return;
        }

        if (!string.Equals(
                existing.UserId,
                userId,
                StringComparison.Ordinal))
        {
            throw new HubException(
                "Connection identity mismatch.");
        }

        _registry.RemoveIfUnchanged(existing);

        await Groups.RemoveFromGroupAsync(
            Context.ConnectionId,
            GroupName(
                existing.TripPlanId,
                existing.SessionId),
            Context.ConnectionAborted);
    }

    // ==============================================
    // CONNECTION CLEANUP
    // ==============================================

    public override async Task OnDisconnectedAsync(
        Exception? exception)
    {
        _registry.Remove(
            Context.ConnectionId);

        await base.OnDisconnectedAsync(exception);
    }
}
