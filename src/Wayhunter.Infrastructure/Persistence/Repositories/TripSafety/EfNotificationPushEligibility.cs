using Microsoft.EntityFrameworkCore;
using Wayhunter.Application.Modules.TripSafety.Notifications;
using Wayhunter.Domain.Modules.IdentitySocial.Enums;
using Wayhunter.Domain.Modules.TravelPlanning.Enums;
using Wayhunter.Domain.Modules.TripSafety.Enums;
using Wayhunter.Infrastructure.Persistence.DbContexts;

namespace Wayhunter.Infrastructure.Persistence.Repositories.TripSafety;

public interface INotificationTargetAccess
{
    // Must revalidate the current recipient's target permission and the originating
    // admin's authority, if present, against trusted application data.
    Task<bool> CanDeliverAsync(string userId, NotificationCategory category, Guid? targetId,
        string? adminActorId, NotificationAudienceRole? role, string? district, CancellationToken ct);
}

// Unregistered until schema and target-policy integration are approved.
public sealed class EfNotificationPushEligibility(IDbContextFactory<WayhunterSharedDbContext> contexts,
    INotificationTargetAccess targetAccess) : INotificationPushEligibility
{
    public async Task<bool> CanPushAsync(NotificationLease lease, CancellationToken ct)
    {
        await using var db = await contexts.CreateDbContextAsync(ct);
        if (!db.Database.IsSqlServer())
            return false;

        var current = await (
            from item in db.NotificationRecipients
            join evt in db.NotificationEvents on item.EventId equals evt.EventId
            join user in db.Users on item.UserId equals user.UserId
            where item.EventId == lease.EventId && item.UserId == lease.UserId && item.LeaseToken == lease.Token &&
                item.State == NotificationDeliveryState.Pending &&
                user.Status == UserAccountStatus.Active && user.VerifiedAtUtc != null && user.DeletionRequestedAtUtc == null &&
                db.NotificationPreferences.Any(p => p.UserId == item.UserId && p.Category == evt.Category && p.PushEnabled) &&
                db.NotificationDevices.Any(d => d.UserId == item.UserId && d.PushAllowed && d.RevokedAtUtc == null)
            select new { evt.JobId, evt.TargetId, evt.Category, evt.Template, evt.TemplateVersion }).SingleOrDefaultAsync(ct);
        if (current is null || current.Category != lease.Category || current.Template != lease.Template ||
            current.TemplateVersion != lease.TemplateVersion)
            return false;

        if (current.JobId is null)
            return await targetAccess.CanDeliverAsync(lease.UserId, lease.Category, current.TargetId, null, null, null, ct);

        var job = await db.NotificationJobs.AsNoTracking().SingleOrDefaultAsync(x =>
            x.JobId == current.JobId && x.State == NotificationJobState.Published, ct);
        if (job is null)
            return false;
        if (job.Kind == NotificationJobKind.AdminMessage)
            return await targetAccess.CanDeliverAsync(lease.UserId, lease.Category, current.TargetId,
                job.ActorId, job.AudienceRole, job.AudienceDistrict, ct);
        if (job.Kind != NotificationJobKind.TripReminder || job.ActorId != lease.UserId ||
            job.TripPlanId != current.TargetId || lease.Template != NotificationTemplate.TripReminder)
            return false;

        var trip = await db.Trips.Where(x => x.Id == job.TripPlanId && x.OwnerUserId == lease.UserId &&
                x.DraftStatus != TripDraftStatus.Cancelled && x.StartDate == job.PlannedStartDate)
            .Select(x => x.RowVersion).SingleOrDefaultAsync(ct);
        return trip is not null && Convert.ToHexString(trip) == job.TripVersion &&
            await db.LiveTripSessions.AnyAsync(x => x.TripPlanId == job.TripPlanId && x.SessionId == job.SessionId &&
                (x.Status == LiveTripStatus.Active || x.Status == LiveTripStatus.Paused), ct);
    }
}
