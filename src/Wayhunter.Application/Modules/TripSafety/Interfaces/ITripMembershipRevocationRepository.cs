
using System;
using System.Threading;
using System.Threading.Tasks;
using Wayhunter.Domain.Modules.TravelPlanning.Entities;
using Wayhunter.Domain.Modules.TripSafety.Entities;

namespace Wayhunter.Application.Modules.TripSafety.Interfaces;

public interface ITripMembershipRevocationRepository
{
    // Load the trip from trusted backend storage.
    // The service will validate owner permission.

    Task<TripPlan?> GetTripPlanByIdAsync(
        Guid tripPlanId,
        CancellationToken cancellationToken = default);

    // Find an existing membership for this trip and user.
    // Return null if the membership does not exist.

    Task<TripMember?> GetMemberAsync(
        Guid tripPlanId,
        string memberUserId,
        CancellationToken cancellationToken = default);

    // Atomically deactivate an active membership
    // and record one revocation notification event.
    //
    // Returns:
    // true  - membership was removed successfully.
    // false - membership was already inactive.
    //
    // Requirements for the implementation:
    // - Never delete historical membership records.
    // - Prevent duplicate removal notifications.
    // - Handle simultaneous removal requests safely.
    // - Use the trusted server removal timestamp.
    // - Do not automatically grant or restore GPS consent.
    // - Persist removal and notification together.
    //
    // Production SQL transaction and outbox
    // implementation require shared DB approval.

    Task<bool> RemoveMemberAndQueueNotificationAsync(
        Guid tripPlanId,
        Guid memberId,
        DateTimeOffset removedAtUtc,
        CancellationToken cancellationToken = default);
}
