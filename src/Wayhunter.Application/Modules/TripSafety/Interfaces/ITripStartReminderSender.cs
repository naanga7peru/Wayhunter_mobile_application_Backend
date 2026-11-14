
using System.Threading;
using System.Threading.Tasks;

namespace Wayhunter.Application.Modules.TripSafety.Interfaces;

public interface ITripStartReminderSender
{
    // Send notification only.
    // This interface must never start a trip session.
    Task SendAsync(
        string tripPlanId,
        string recipientUserId,
        CancellationToken cancellationToken = default);
}
