using Wayhunter.Api.Contracts.HomeAdsAnalytics;
using Wayhunter.Application.Modules.TripSafety.Notifications;
using Wayhunter.Infrastructure.Persistence.Repositories.TripSafety;

namespace Wayhunter.Api.Extensions;

public static class NotificationServiceCollectionExtensions
{
    public const string AdminPolicy = "M3NotificationAdministrator";

    public static IServiceCollection AddNotificationFoundation(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddAuthorization(options => options.AddPolicy(AdminPolicy, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireAssertion(context => configuration["Identity:AdministratorRole"] == "Admin" &&
                AdvertisementAdminPrincipalReadiness.HasAuthenticatedAdminRole(context.User, "Admin"));
        }));
        services.AddScoped<NotificationComposerService>();
        services.AddScoped<NotificationReminderService>();
        services.AddSingleton<INotificationInbox, UnavailableNotifications>();
        services.AddSingleton<INotificationJobs, UnavailableNotifications>();
        services.AddSingleton<INotificationDeviceRegistry, UnavailableNotifications>();
        services.AddSingleton<INotificationAudienceDirectory, UnavailableNotifications>();
        services.AddSingleton<INotificationPushProvider, UnavailableNotificationPushProvider>();
        return services;
    }

    private sealed class UnavailableNotifications : INotificationInbox, INotificationJobs,
        INotificationDeviceRegistry, INotificationAudienceDirectory
    {
        public Task<NotificationInboxPage> ReadAsync(string recipientId, int limit, CancellationToken ct) => throw new NotificationUnavailableException();
        public Task<bool> MarkReadAsync(string recipientId, Guid eventId, DateTimeOffset now, CancellationToken ct) => throw new NotificationUnavailableException();
        public Task<IReadOnlyList<NotificationPreference>> GetPreferencesAsync(string recipientId, CancellationToken ct) => throw new NotificationUnavailableException();
        public Task SetPreferenceAsync(string recipientId, NotificationPreference preference, CancellationToken ct) => throw new NotificationUnavailableException();
        public Task<NotificationDeviceReceipt> RegisterOrRotateAsync(string ownerId, Guid deviceId, Guid operationId, string rawToken,
            bool pushAllowed, CancellationToken ct) => throw new NotificationUnavailableException();
        public Task<bool> RevokeAsync(string ownerId, Guid deviceId, Guid operationId, CancellationToken ct) => throw new NotificationUnavailableException();
        public Task<IReadOnlyList<NotificationAudienceMember>> ResolveAsync(string actorId, NotificationAudienceFilter filter,
            NotificationCategory category, NotificationTarget? target, CancellationToken ct) => throw new NotificationUnavailableException();
        public Task<bool> CanManageAsync(string actorId, CancellationToken ct) => throw new NotificationUnavailableException();
        public Task SavePreviewAsync(NotificationAudienceReceipt receipt, CancellationToken ct) => throw new NotificationUnavailableException();
        public Task<NotificationJobReceipt> ScheduleAdminAsync(NotificationAdminSchedule command, CancellationToken ct) => throw new NotificationUnavailableException();
        public Task<NotificationJobReceipt> ScheduleReminderAsync(NotificationReminder reminder, CancellationToken ct) => throw new NotificationUnavailableException();
        public Task<bool> CancelAsync(NotificationJobKind kind, string ownerId, Guid jobId, int revision, Guid operationId,
            DateTimeOffset now, CancellationToken ct) => throw new NotificationUnavailableException();
        public Task<NotificationDeliverySummary?> ReadSummaryAsync(string actorId, Guid jobId, CancellationToken ct) => throw new NotificationUnavailableException();
    }
}
