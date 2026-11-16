using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
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
        _access = access;
        _registry = registry;
        _fanout = fanout;
        _clock = clock;
    }

    private string AuthenticatedUserId()
    {
        if (Context.User?.Identity?.IsAuthenticated != true)
        {
            throw new HubException(
                "Authentication is required.");
        }

        var userId =
            Context.User.FindFirstValue(
                ClaimTypes.NameIdentifier)
            ?? Context.User.FindFirstValue("sub");

        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new HubException(
                "Trusted user identity is required.");
        }

        return userId.Trim();
    }

    private static string GroupName(
        Guid tripPlanId,
        Guid sessionId)
    {
        return $"trip:{tripPlanId:N}:session:{sessionId:N}";
    }

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

    // Client invokes:
    // JoinTrip(tripPlanId, sessionId)
    //
    // Both membership AND explicit GPS consent
    // are required to receive precise location.

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
            GroupName(tripPlanId, sessionId),
            Context.ConnectionAborted);

        _registry.Register(
            Context.ConnectionId,
            tripPlanId,
            sessionId,
            userId);
    }

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

        // Publisher must still have active membership
        // and active, explicit GPS consent.

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

        // Do not use Clients.Group() for precise GPS.
        // Revalidate every receiver first.

        var recipients =
            await _fanout.GetAuthorizedRecipientsAsync(
                tripPlanId,
                sessionId,
                Context.ConnectionAborted);

        foreach (var recipient in recipients)
        {
            // Recheck immediately before this send.
            // Revoked users are excluded.

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

            // Ensure the connection was not reassigned
            // after recipient selection.
            if (_registry.Find(
                    recipient.ConnectionId) != recipient)
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

    // Allows a revoked member to leave/clean up
    // their existing connection.

    [HubMethodName("LeaveTrip")]
    public async Task LeaveTripAsync()
    {
        var userId = AuthenticatedUserId();

        var existing = _registry.Find(
            Context.ConnectionId);

        if (existing is null)
            return;

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

    public override async Task OnDisconnectedAsync(
        Exception? exception)
    {
        _registry.Remove(Context.ConnectionId);

        await base.OnDisconnectedAsync(exception);
    }
}
