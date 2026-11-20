using System;
using System.Threading;
using System.Threading.Tasks;
namespace Wayhunter.Application.Modules.TripSafety.Interfaces;
public interface ITripFinishedReminderGuard
{
    // An affirmative result requires a persisted, open session for this exact trip.
    Task<bool> CanDispatchAsync(Guid tripPlanId, Guid sessionId, CancellationToken cancellationToken = default);
}
