
using System;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Wayhunter.Application.Modules.TripSafety.Interfaces;

using Wayhunter.Infrastructure.Persistence.Repositories.TripSafety;

using Wayhunter.Api.Services.TripSafety;

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

        // Reuse shared SQL DbContext.
        // This does not create or migrate the database.
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

        services.RemoveAll<ITripInvitationCreateStore>();
        services.AddScoped<ITripInvitationCreateStore, EfTripInvitationCreateStore>();
        services.RemoveAll<ITripInviteEligibilityAccess>();
        services.AddScoped<ITripInviteEligibilityAccess, SqlTripInviteEligibilityAccess>();
        services.RemoveAll<ITripInviteBlockRelationshipReader>();
        services.AddScoped<ITripInviteBlockRelationshipReader, SqlTripInviteBlockRelationshipReader>();

        // M3 - Membership Revocation
        services.RemoveAll<ITripMembershipRevocationRepository>();

        services.AddScoped<
            ITripMembershipRevocationRepository,
            EfTripMembershipRevocationRepository>();

        // M3 - GPS Consent
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

        // M3 - Hazard Persistence
        // New SQL fields require an approved migration.
        services.RemoveAll<IHazardReportRepository>();

        services.AddScoped<
            IHazardReportRepository,
            EfHazardReportRepository>();

        return services;
    }

    public static IServiceCollection AddConfiguredTripSafetyPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        if (configuration.GetValue<bool>(SqlPersistenceFlag))
        {
            services.AddTripSafetySqlPersistence(configuration);
        }

        return services;
    }
}
