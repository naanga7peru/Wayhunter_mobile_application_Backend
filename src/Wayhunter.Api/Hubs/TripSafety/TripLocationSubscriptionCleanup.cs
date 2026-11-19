
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace Wayhunter.Api.Hubs.TripSafety;

public sealed class TripLocationSubscriptionCleanup
{
    private readonly TripLocationSubscriberRegistry _registry;
    private readonly IHubContext<TripLocationHub> _hub;
    private readonly ILogger<TripLocationSubscriptionCleanup> _logger;

    public TripLocationSubscriptionCleanup(
        TripLocationSubscriberRegistry registry,
        IHubContext<TripLocationHub> hub,
        ILogger<TripLocationSubscriptionCleanup> logger)
    {
        _registry = registry
            ?? throw new ArgumentNullException(nameof(registry));

        _hub = hub
            ?? throw new ArgumentNullException(nameof(hub));

        _logger = logger
            ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task RevokeSessionAsync(
        Guid tripPlanId,
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var removed = _registry.RevokeSession(
            tripPlanId,
            sessionId);

        return NotifyAndRemoveGroupsAsync(
            removed,
            cancellationToken);
    }

    public Task RevokeTripMemberAsync(
        Guid tripPlanId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var removed = _registry.RevokeTripMember(
            tripPlanId,
            userId);

        return NotifyAndRemoveGroupsAsync(
            removed,
            cancellationToken);
    }

    public Task RevokeMemberFromSessionAsync(
        Guid tripPlanId,
        Guid sessionId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var removed = _registry.RevokeMemberFromSession(
            tripPlanId,
            sessionId,
            userId);

        return NotifyAndRemoveGroupsAsync(
            removed,
            cancellationToken);
    }

    private async Task NotifyAndRemoveGroupsAsync(
        IReadOnlyList<TripLocationSubscriber> removed,
        CancellationToken cancellationToken)
    {
        foreach (var subscriber in removed)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var groupName =
                $"trip:{subscriber.TripPlanId:N}:" +
                $"session:{subscriber.SessionId:N}";

            try
            {
                await _hub.Groups.RemoveFromGroupAsync(
                    subscriber.ConnectionId,
                    groupName,
                    cancellationToken);

                await _hub.Clients.Client(
                    subscriber.ConnectionId).SendAsync(
                    "TripLocationAccessRevoked",
                    new
                    {
                        subscriber.TripPlanId,
                        subscriber.SessionId
                    },
                    cancellationToken);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                // Registry cleanup already happened.
                // Do not restore revoked GPS access.
                _logger.LogWarning(
                    exception,
                    "M3 SignalR subscription cleanup failed.");
            }
        }
    }
}
