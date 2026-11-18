
using System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Wayhunter.Api.Contracts.HomeAdsAnalytics;

namespace Wayhunter.Api.Authorization;

public static class AdvertisementAdministratorPolicy
{
    public const string Name =
        "M7AdvertisementAdministrator";

    public static IServiceCollection
        AddAdvertisementAdministratorPolicy(
            this IServiceCollection services,
            IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddAuthorization(options =>
        {
            options.AddPolicy(Name, policy =>
            {
                policy.RequireAuthenticatedUser();

                policy.RequireAssertion(context =>
                {
                    var configuredRole = configuration[
                        "Identity:AdministratorRole"];

                    // Approved M1/M7 role contract.
                    // A missing or unexpected configuration
                    // must never grant Administrator access.
                    if (!string.Equals(
                            configuredRole,
                            "Admin",
                            StringComparison.Ordinal))
                    {
                        return false;
                    }

                    // Valid trusted identity and role claim
                    // are both required.
                    return AdvertisementAdminPrincipalReadiness
                        .HasAuthenticatedAdminRole(
                            context.User,
                            configuredRole);
                });
            });
        });

        return services;
    }
}
