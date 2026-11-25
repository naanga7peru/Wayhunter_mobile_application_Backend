using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wayhunter.Application.Modules.TripSafety.Memories;
using Wayhunter.Application.Modules.TripSafety.Notifications;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Wayhunter.Infrastructure.Persistence.Models.TripSafety;
using Wayhunter.Infrastructure.Persistence.Repositories.TripSafety;
using Xunit;

namespace Wayhunter.IntegrationTests.Modules.IdentitySocial;

public sealed partial class LocalDbAuthenticationPhase3Tests
{
    private static void AddSimulatedTripSafetyProviders(IServiceCollection services)
    {
        services.RemoveAll<ITripMemoryMediaProvider>();
        services.AddSingleton<ITripMemoryMediaProvider, SimulatedTripMemoryMedia>();
        services.RemoveAll<INotificationInbox>();
        services.RemoveAll<INotificationJobs>();
        services.RemoveAll<INotificationDeviceRegistry>();
        services.RemoveAll<INotificationAudienceDirectory>();
        services.AddScoped<EfNotificationWorkflowStore>();
        services.AddScoped<INotificationInbox>(sp => sp.GetRequiredService<EfNotificationWorkflowStore>());
        services.AddScoped<INotificationJobs>(sp => sp.GetRequiredService<EfNotificationWorkflowStore>());
        services.AddScoped<INotificationDeviceRegistry, EfNotificationDeviceRegistry>();
        services.AddScoped<INotificationAudienceDirectory, SyntheticAudienceDirectory>();
        services.AddSingleton<INotificationTokenProtector, SyntheticDeviceProtector>();
    }

    private static async Task VerifySimulatedNotificationsAsync(WayhunterSharedDbContext db,
        Func<string, string, int, string?, object?, Task<JsonElement>> check, Action<string?> use,
        string ownerId, string ownerToken, string outsiderToken, string adminToken,
        NotificationAdminCommand command, NotificationReminderCommand reminder)
    {
        const string mobile = "/api/mobile/notifications";
        const string admin = "/api/admin/notifications";
        var eventId = Guid.NewGuid();
        db.NotificationEvents.Add(new NotificationEventRow
        {
            EventId = eventId, Category = NotificationCategory.Safety, Template = NotificationTemplate.SafetyUpdate,
            TemplateVersion = 1, CreatedAtUtc = DateTimeOffset.UtcNow
        });
        db.NotificationRecipients.Add(new NotificationRecipientRow
        {
            EventId = eventId, UserId = ownerId, State = NotificationDeliveryState.Delivered,
            CompletedAtUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        use(outsiderToken);
        var outside = await check("GET", mobile, 200, null, null);
        Assert.Empty(outside.GetProperty("items").EnumerateArray());
        await check("PUT", mobile + "/{eventId}/read", 404, $"{mobile}/{eventId}/read", null);
        use(ownerToken);
        var inbox = await check("GET", mobile, 200, null, null);
        Assert.Equal(eventId, Assert.Single(inbox.GetProperty("items").EnumerateArray()).GetProperty("eventId").GetGuid());
        await check("PUT", mobile + "/{eventId}/read", 204, $"{mobile}/{eventId}/read", null);
        Assert.True(await db.NotificationRecipients.AnyAsync(x => x.EventId == eventId && x.UserId == ownerId && x.ReadAtUtc != null));
        await check("PUT", mobile + "/preferences", 204, null, new { category = 1, pushEnabled = true });
        var preferences = await check("GET", mobile + "/preferences", 200, null, null);
        Assert.Contains(preferences.EnumerateArray(), x => x.GetProperty("category").GetInt32() == 1 && x.GetProperty("pushEnabled").GetBoolean());
        Assert.True(await db.NotificationPreferences.AnyAsync(x => x.UserId == ownerId && x.Category == NotificationCategory.Trip && x.PushEnabled));

        var deviceId = Guid.NewGuid();
        var registered = await check("PUT", mobile + "/devices/{deviceId}", 200, $"{mobile}/devices/{deviceId}",
            new { operationId = Guid.NewGuid(), token = SyntheticDeviceProtector.FixtureToken, pushAllowed = true });
        Assert.False(registered.TryGetProperty("token", out _));
        Assert.True(await db.NotificationDevices.AnyAsync(x => x.DeviceId == deviceId && x.UserId == ownerId && x.PushAllowed));
        await check("POST", mobile + "/devices/{deviceId}/revoke", 204, $"{mobile}/devices/{deviceId}/revoke",
            new { operationId = Guid.NewGuid() });
        Assert.True(await db.NotificationDevices.AnyAsync(x => x.DeviceId == deviceId && x.RevokedAtUtc != null));

        var reminderJob = await check("PUT", mobile + "/reminders", 200, null, reminder);
        var reminderId = reminderJob.GetProperty("jobId").GetGuid();
        await check("POST", mobile + "/reminders/{jobId}/cancel", 204, $"{mobile}/reminders/{reminderId}/cancel",
            new { operationId = Guid.NewGuid(), revision = reminderJob.GetProperty("revision").GetInt32() });
        Assert.True(await db.NotificationJobs.AnyAsync(x => x.JobId == reminderId && x.State == NotificationJobState.Cancelled));

        use(adminToken);
        var preview = await check("POST", admin + "/preview", 200, null, command);
        var previewId = preview.GetProperty("receiptId").GetGuid();
        Assert.True(await db.NotificationAudienceReceipts.AnyAsync(x => x.ReceiptId == previewId && x.Count == 1));
        var schedule = await check("POST", admin + "/schedules", 200, null, new { command, confirmBulk = false });
        var scheduledId = schedule.GetProperty("jobId").GetGuid();
        Assert.True(await db.NotificationJobRecipients.AnyAsync(x => x.JobId == scheduledId && x.UserId == command.Audience.UserIds![0]));
        await check("GET", admin + "/schedules/{jobId}/summary", 200, $"{admin}/schedules/{scheduledId}/summary", null);
        await check("POST", admin + "/schedules/{jobId}/cancel", 204, $"{admin}/schedules/{scheduledId}/cancel",
            new { operationId = Guid.NewGuid(), revision = schedule.GetProperty("revision").GetInt32() });
        Assert.True(await db.NotificationJobs.AnyAsync(x => x.JobId == scheduledId && x.State == NotificationJobState.Cancelled));
    }

    private sealed class SimulatedTripMemoryMedia : ITripMemoryMediaProvider
    {
        private static readonly byte[] Png = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a+wAAAABJRU5ErkJggg==");
        public static int PngLength => Png.Length;

        public Task<MemoryUploadGrant> IssueAsync(TripMemoryMedia operation, CancellationToken ct) =>
            Task.FromResult(new MemoryUploadGrant(operation.OperationId, new Uri("https://example.invalid/upload"),
                operation.CreatedAtUtc.AddMinutes(5)));

        public Task<VerifiedMemoryAsset?> InspectAsync(TripMemoryMedia operation, CancellationToken ct) =>
            Task.FromResult<VerifiedMemoryAsset?>(new(operation.OperationId, operation.MemoryId, operation.OwnerUserId,
                "synthetic/" + operation.OperationId, "v1", operation.ContentType, Png.Length, true, true));

        public Task<Stream> OpenVerifiedContentAsync(VerifiedMemoryAsset asset, CancellationToken ct) =>
            Task.FromResult<Stream>(new MemoryStream(Png, writable: false));

        public Task DeleteVerifiedVersionAsync(VerifiedMemoryAsset asset, CancellationToken ct) =>
            throw new InvalidOperationException("This test never deletes provider data.");
    }

    private sealed class SyntheticAudienceDirectory(WayhunterSharedDbContext db) : INotificationAudienceDirectory
    {
        public Task<bool> CanManageAsync(string actorId, CancellationToken ct) =>
            db.UserRoleAssignments.AnyAsync(x => x.UserId == actorId && x.Role == "Admin" && x.RevokedAtUtc == null, ct);

        public async Task<IReadOnlyList<NotificationAudienceMember>> ResolveAsync(string actorId,
            NotificationAudienceFilter filter, NotificationCategory category, NotificationTarget? target, CancellationToken ct)
        {
            if (target is not null || filter.Role is not null || filter.District is not null ||
                filter.UserIds is not { Length: 1 } requested || !await CanManageAsync(actorId, ct))
                throw new UnauthorizedAccessException();

            return await db.Users.Where(x => x.UserId == requested[0] &&
                    x.Status == Wayhunter.Domain.Modules.IdentitySocial.Enums.UserAccountStatus.Active)
                .Select(x => new NotificationAudienceMember(x.UserId, NotificationAudienceRole.Member, null))
                .ToListAsync(ct);
        }
    }

    private sealed class SyntheticDeviceProtector : INotificationTokenProtector
    {
        public const string FixtureToken = "synthetic-device-token";

        public Task<ProtectedNotificationToken> VerifyAndProtectAsync(string ownerId, Guid deviceId, string rawToken, CancellationToken ct) =>
            rawToken == FixtureToken
                ? Task.FromResult(new ProtectedNotificationToken(Convert.ToHexString(deviceId.ToByteArray().Concat(deviceId.ToByteArray()).ToArray()), [1, 2, 3]))
                : throw new UnauthorizedAccessException();
    }
}
