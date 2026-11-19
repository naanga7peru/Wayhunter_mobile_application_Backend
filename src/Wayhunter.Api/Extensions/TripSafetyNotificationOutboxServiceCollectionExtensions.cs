
using System;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Wayhunter.Api.Services.TripSafety;

using Wayhunter.Infrastructure.Integrations.Notifications;
using Wayhunter.Infrastructure.Persistence.Repositories.TripSafety;

namespace Wayhunter.Api.Extensions;

public static class TripSafetyNotificationOutboxServiceCollectionExtensions
{
    public static IServiceCollection
        AddConfiguredTripSafetyNotificationOutboxWorker(
            this IServiceCollection services,
            IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var enabled = configuration.GetValue<bool>(
            "TripSafety:EnableNotificationOutboxWorker");

        if (!enabled)
        {
            // No background service or sender is created.
            return services;
        }

        if (!configuration.GetValue<bool>(
                "TripSafety:EnableSqlPersistence"))
        {
            throw new InvalidOperationException(
                "M3 notification worker requires M3 SQL persistence.");
        }

        var settings = configuration
            .GetSection("TripSafety:NotificationOutbox")
            .Get<M3OutboxWorkerSettings>()
            ?? new M3OutboxWorkerSettings();

        settings.Validate();

        var webhook = configuration
            .GetSection("TripSafety:NotificationOutbox")
            .Get<M3OutboxWebhookSettings>()
            ?? new M3OutboxWebhookSettings();

        webhook.Validate();

        services.AddSingleton(settings);
        services.AddSingleton(webhook);

        // HTTP timeout must be shorter than SQL lease.
        services.AddHttpClient<
            ITripMembershipRevocationNotificationSender,
            WebhookTripMembershipRevocationNotificationSender>(
            client =>
            {
                client.Timeout = TimeSpan.FromSeconds(25);
            });

        services.AddScoped<
            M3NotificationOutboxDispatcher>();

        services.AddHostedService<
            M3NotificationOutboxWorker>();

        return services;
    }
}
