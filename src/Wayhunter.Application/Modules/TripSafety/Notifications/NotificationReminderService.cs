using Wayhunter.Application.Modules.TravelPlanning.Interfaces;
using Wayhunter.Application.Modules.TripSafety.Interfaces;
using Wayhunter.Domain.Modules.TravelPlanning.Enums;

namespace Wayhunter.Application.Modules.TripSafety.Notifications;

public sealed class NotificationReminderService(ITripPlanRepository trips, ITripFinishedReminderGuard guard,
    INotificationJobs jobs, TimeProvider clock)
{
    public async Task<NotificationJobReceipt> ScheduleAsync(string userId, NotificationReminderCommand command,
        CancellationToken ct)
    {
        if (!NotificationInputs.IsCanonicalUser(userId))
            throw new UnauthorizedAccessException();
        NotificationInputs.CheckOperation(command.TripPlanId, command.OperationId);
        if (command.SessionId == Guid.Empty || command.DueAtUtc.Offset != TimeSpan.Zero ||
            command.DueAtUtc <= clock.GetUtcNow())
            throw new ArgumentException("A session and future UTC reminder time are required.");

        var trip = await trips.GetByIdAsync(command.TripPlanId, ct);
        if (trip is null || trip.Id != command.TripPlanId || trip.OwnerUserId != userId)
            throw new KeyNotFoundException();
        if (trip.DraftStatus == TripDraftStatus.Cancelled || trip.StartDate != command.PlannedStartDate ||
            trip.RowVersion.Length != 8 || !await guard.CanDispatchAsync(command.TripPlanId, command.SessionId, ct))
            throw new NotificationConflictException();

        return await jobs.ScheduleReminderAsync(new(command, userId, Convert.ToHexString(trip.RowVersion)), ct);
    }

    public Task<bool> CancelAsync(string userId, Guid jobId, int revision, Guid operationId, CancellationToken ct)
    {
        if (!NotificationInputs.IsCanonicalUser(userId))
            throw new UnauthorizedAccessException();
        NotificationInputs.CheckOperation(jobId, operationId, revision);
        return jobs.CancelAsync(NotificationJobKind.TripReminder, userId, jobId, revision, operationId,
            clock.GetUtcNow(), ct);
    }
}

public sealed class NotificationReminderExecutionService(ITripPlanRepository trips, ITripFinishedReminderGuard guard,
    INotificationReminderPublisher publisher, TimeProvider clock)
{
    public async Task<bool> TryPublishAsync(Guid jobId, int revision, NotificationReminder reminder, CancellationToken ct)
    {
        NotificationInputs.CheckOperation(jobId, reminder.Command.OperationId, revision);
        var command = reminder.Command;
        var now = clock.GetUtcNow();
        if (command.DueAtUtc > now)
            return false;

        var trip = await trips.GetByIdAsync(command.TripPlanId, ct);
        if (trip is null || trip.Id != command.TripPlanId || trip.OwnerUserId != reminder.UserId ||
            trip.DraftStatus == TripDraftStatus.Cancelled || trip.StartDate != command.PlannedStartDate ||
            trip.RowVersion.Length != 8 || Convert.ToHexString(trip.RowVersion) != reminder.TripVersion ||
            !await guard.CanDispatchAsync(command.TripPlanId, command.SessionId, ct))
            return false;

        return await publisher.PublishIfCurrentAsync(jobId, revision, reminder, now, ct);
    }
}
