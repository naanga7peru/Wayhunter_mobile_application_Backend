using System;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.TripSafety.Interfaces;
namespace Wayhunter.Application.Modules.TripSafety.Services;
public sealed class TripStartReminderDispatchService
{
    private readonly ITripStartReminderSender _sender;
    private readonly ITripFinishedReminderGuard? _finishedGuard;
    public TripStartReminderDispatchService(ITripStartReminderSender sender, ITripFinishedReminderGuard? finishedGuard = null)
    {
        _sender = sender ?? throw new ArgumentNullException(nameof(sender));
        _finishedGuard = finishedGuard;
    }
    public async Task DispatchAsync(Guid tripPlanId, string? recipientUserId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (tripPlanId == Guid.Empty) throw new ArgumentException("Valid Trip Plan ID is required.", nameof(tripPlanId));
        if (string.IsNullOrWhiteSpace(recipientUserId)) throw new ArgumentException("Recipient User ID is required.", nameof(recipientUserId));
        if (_finishedGuard is not null && await _finishedGuard.HasFinishedAsync(tripPlanId, cancellationToken)) return;
        await _sender.SendAsync(tripPlanId, recipientUserId.Trim(), cancellationToken);
    }
}
