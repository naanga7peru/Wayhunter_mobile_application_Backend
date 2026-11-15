
using System;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Application.Modules.TripSafety.Interfaces;

namespace Wayhunter.Application.Modules.TripSafety.Services;

public sealed class TripStartReminderDispatchService
{
    private readonly ITripStartReminderSender _sender;

    public TripStartReminderDispatchService(
        ITripStartReminderSender sender)
    {
        _sender = sender
            ?? throw new ArgumentNullException(nameof(sender));
    }

    // Dispatches a reminder notification only.
    // Does not create, resume or modify a trip session.
    //
    // Caller must verify:
    // - Trip ownership and intended recipient
    // - Notification preferences
    // - Reminder eligibility and due time
    // - Current trip state
    //
    // TripPlanId uses Guid to match M2 TripPlan.Id.

    public async Task DispatchAsync(
        Guid tripPlanId,
        string? recipientUserId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (tripPlanId == Guid.Empty)
        {
            throw new ArgumentException(
                "Valid Trip Plan ID is required.",
                nameof(tripPlanId));
        }

        if (string.IsNullOrWhiteSpace(recipientUserId))
        {
            throw new ArgumentException(
                "Recipient User ID is required.",
                nameof(recipientUserId));
        }

        await _sender.SendAsync(
            tripPlanId,
            recipientUserId.Trim(),
            cancellationToken);
    }
}
