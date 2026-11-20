using System;
using System.Threading;
using System.Threading.Tasks;
namespace Wayhunter.Application.Modules.TripSafety.Interfaces;
public interface ITripFinishedReminderGuard
{
    Task MarkFinishedAsync(Guid tripPlanId, Guid sessionId, CancellationToken cancellationToken = default);
    Task<bool> HasFinishedAsync(Guid tripPlanId, CancellationToken cancellationToken = default);
}
