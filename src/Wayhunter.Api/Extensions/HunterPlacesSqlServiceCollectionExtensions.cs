
using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wayhunter.Api.Services.HunterPlaces;
using Wayhunter.Application.Modules.HunterPlaces.Interfaces;
using Wayhunter.Infrastructure.Persistence.Repositories.HunterPlaces;

namespace Wayhunter.Api.Extensions;

public static class HunterPlacesSqlServiceCollectionExtensions
{
    private const string SqlPersistenceFlag =
        "HunterPlaces:EnableSqlPersistence";

    public static IServiceCollection AddHunterPlacesSqlPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // Reuse the existing shared SQL DbContext.
        // This method does not execute database migrations.
        services.AddWayhunterSharedPersistence(configuration);

        // Use one scoped instance for both capture-session interfaces.
        services.RemoveAll<IHunterCaptureSessionStore>();
        services.RemoveAll<IHunterCaptureSessionRevocationStore>();
        services.RemoveAll<SqlHunterCaptureSessionStore>();

        services.AddScoped<SqlHunterCaptureSessionStore>();

        services.AddScoped<IHunterCaptureSessionStore>(
            sp => sp.GetRequiredService<
                SqlHunterCaptureSessionStore>());

        services.AddScoped<IHunterCaptureSessionRevocationStore>(
            sp => sp.GetRequiredService<
                SqlHunterCaptureSessionStore>());

        // Trusted submission ownership and status reader.
        services.RemoveAll<IHunterCaptureSubmissionAccessReader>();

        services.TryAddScoped<SqlHunterCaptureSubmissionAccessReader>();
        services.AddScoped<IHunterCaptureSubmissionAccessReader>(
            sp => sp.GetRequiredService<SqlHunterCaptureSubmissionAccessReader>());
        services.TryAddScoped<IHunterOwnedSubmissionReader>(
            sp => sp.GetRequiredService<SqlHunterCaptureSubmissionAccessReader>());

        // Atomic status transition and session revocation.
        services.RemoveAll<IHunterSubmissionStatusTransitionStore>();

        services.AddScoped<
            IHunterSubmissionStatusTransitionStore,
            SqlHunterSubmissionStatusTransitionStore>();

        return services;
    }

    public static IServiceCollection AddConfiguredHunterPlacesPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // SQL persistence remains disabled unless explicitly enabled.
        if (!configuration.GetValue<bool>(SqlPersistenceFlag))
        {
            return services;
        }

        // M4 SQL requires M1 authenticated identity.
        if (!configuration.GetValue<bool>(
                "Identity:EnableSharedAuthentication"))
        {
            throw new InvalidOperationException(
                "M4 SQL persistence requires M1 shared authentication.");
        }

        services.AddHunterPlacesSqlPersistence(configuration);
        services.AddHunterPlacesApplication();
        services.TryAddScoped<HunterDraftCreationService>();

        return services;
    }
}
