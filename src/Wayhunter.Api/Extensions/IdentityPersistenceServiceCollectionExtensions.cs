
using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wayhunter.Api.Services.IdentitySocial;

namespace Wayhunter.Api.Extensions;

public static class IdentityPersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddWayhunterIdentityPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // M1 uses the single shared database context.
        services.AddWayhunterSharedPersistence(configuration);

        services.TryAddScoped<
            IAuthenticatedSessionReader,
            SqlAuthenticatedSessionReader>();

        services.TryAddScoped<
            IIdentitySessionRevocationStore,
            SqlIdentitySessionRevocationStore>();

        services.TryAddScoped<SessionLogoutService>();

        return services;
    }
}
