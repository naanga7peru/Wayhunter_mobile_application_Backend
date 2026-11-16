using System;
using System.Threading;
using System.Threading.Tasks;

namespace Wayhunter.Application.Modules.TripSafety.Interfaces;

public interface ITripLiveLocationAccessReader
{
    Task EnsureAllowedAsync(
        Guid tripPlanId,
        Guid sessionId,
        string authenticatedUserId,
        CancellationToken cancellationToken = default);
}
