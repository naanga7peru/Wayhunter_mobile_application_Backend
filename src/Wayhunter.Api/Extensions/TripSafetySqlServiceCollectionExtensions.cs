
using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Wayhunter.Application.Modules.TripSafety.Interfaces;

using Wayhunter.Infrastructure.Persistence.Repositories.TripSafety;

namespace Wayhunter.Api.Extensions;

public static class TripSafetySqlServiceCollectionExtensions
{
    private const string SqlPersistenceFlag =
        "TripSafety:EnableSqlPersistence";

    public static IServiceCollection AddTripSafetySqlPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // Reuse the shared SQL DbContext registration.
        // This does NOT create or migrate database tables.
        services.AddWayhunterSharedPersistence(configuration);

        // M3 - Live Trip Sessions
        services.RemoveAll<ILiveTripSessionRepository>();

        services.AddScoped<
            ILiveTripSessionRepository,
            EfLiveTripSessionRepository>();

        // M3 - Invitation Decisions
        services.RemoveAll<ITripInvitationDecisionRepository>();

        services.AddScoped<
            ITripInvitationDecisionRepository,
            EfTripInvitationDecisionRepository>();

        // M3 - Membership Revocation
        services.RemoveAll<ITripMembershipRevocationRepository>();

        services.AddScoped<
            ITripMembershipRevocationRepository,
            EfTripMembershipRevocationRepository>();

        // M3 - GPS Consent
        // One scoped SQL repository implements both contracts.
        services.RemoveAll<ITripSharingConsentRepository>();
        services.RemoveAll<ITripLiveLocationAccessReader>();
        services.RemoveAll<EfTripSharingConsentRepository>();

        services.AddScoped<EfTripSharingConsentRepository>();

        services.AddScoped<ITripSharingConsentRepository>(
            sp => sp.GetRequiredService<
                EfTripSharingConsentRepository>());

        services.AddScoped<ITripLiveLocationAccessReader>(
            sp => sp.GetRequiredService<
                EfTripSharingConsentRepository>());

        return services;
    }

    public static IServiceCollection AddConfiguredTripSafetyPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var enableSql =
            configuration.GetValue<bool>(SqlPersistenceFlag);

        if (enableSql)
        {
            services.AddTripSafetySqlPersistence(configuration);
        }

        return services;
    }
}
