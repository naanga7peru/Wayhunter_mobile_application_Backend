
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Wayhunter.Application.Modules.TripSafety.Interfaces;

public interface ITripStartReminderSender
{
    // Send a notification only.
    // This interface must never start a trip session.
    //
    // TripPlanId uses Guid to match
    // M2 TravelPlanning.TripPlan.Id.

    Task SendAsync(
        Guid tripPlanId,
        Guid sessionId,
        string recipientUserId,
        CancellationToken cancellationToken = default);
}
