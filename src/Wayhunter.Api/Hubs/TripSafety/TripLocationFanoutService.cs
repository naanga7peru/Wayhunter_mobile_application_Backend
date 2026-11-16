using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.TripSafety.Interfaces;

namespace Wayhunter.Api.Hubs.TripSafety;

public sealed class TripLocationFanoutService
{
    private readonly ITripLiveLocationAccessReader _access;
    private readonly TripLocationSubscriberRegistry _registry;

    public TripLocationFanoutService(
        ITripLiveLocationAccessReader access,
        TripLocationSubscriberRegistry registry)
    {
        _access = access
            ?? throw new ArgumentNullException(nameof(access));

        _registry = registry
            ?? throw new ArgumentNullException(nameof(registry));
    }

    public async Task<
        IReadOnlyList<TripLocationSubscriber>>
        GetAuthorizedRecipientsAsync(
            Guid tripPlanId,
            Guid sessionId,
            CancellationToken cancellationToken = default)
    {
        var candidates = _registry.Snapshot(
            tripPlanId, sessionId);

        var allowed = new List<TripLocationSubscriber>();

        foreach (var subscriber in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                // Every recipient is checked again.
                await _access.EnsureAllowedAsync(
                    tripPlanId,
                    sessionId,
                    subscriber.UserId,
                    cancellationToken);

                allowed.Add(subscriber);
            }
            catch (UnauthorizedAccessException)
            {
                _registry.RemoveIfUnchanged(subscriber);
            }
            catch (InvalidOperationException)
            {
                _registry.RemoveIfUnchanged(subscriber);
            }
            catch (KeyNotFoundException)
            {
                _registry.RemoveIfUnchanged(subscriber);
            }
        }

        return allowed;
    }
}
