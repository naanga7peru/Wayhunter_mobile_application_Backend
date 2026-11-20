using System;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.TripSafety.Interfaces;
namespace Wayhunter.Application.Modules.TripSafety.Services;
public sealed class TripStartReminderDispatchService
{
    private readonly ITripStartReminderSender _sender;
    private readonly ITripFinishedReminderGuard _finishedGuard;
    public TripStartReminderDispatchService(ITripStartReminderSender sender, ITripFinishedReminderGuard finishedGuard)
    {
        _sender = sender ?? throw new ArgumentNullException(nameof(sender));
        _finishedGuard = finishedGuard ?? throw new ArgumentNullException(nameof(finishedGuard));
    }
    public async Task DispatchAsync(Guid tripPlanId, Guid sessionId, string? recipientUserId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (tripPlanId == Guid.Empty) throw new ArgumentException("Valid Trip Plan ID is required.", nameof(tripPlanId));
        if (sessionId == Guid.Empty) throw new ArgumentException("Valid Session ID is required.", nameof(sessionId));
        if (string.IsNullOrWhiteSpace(recipientUserId)) throw new ArgumentException("Recipient User ID is required.", nameof(recipientUserId));
        if (!await _finishedGuard.CanDispatchAsync(tripPlanId, sessionId, cancellationToken)) return;
        cancellationToken.ThrowIfCancellationRequested();
        await _sender.SendAsync(tripPlanId, sessionId, recipientUserId.Trim(), cancellationToken);
    }
}
