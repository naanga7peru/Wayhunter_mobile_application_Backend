using Microsoft.AspNetCore.Authorization;
using Wayhunter.Api.Contracts.HomeAdsAnalytics;

namespace Wayhunter.Api.Authorization;

public static class CoinsWalletAdministratorPolicy
{
    public const string Name = "M6CoinsWalletAdministrator";

    public static IServiceCollection AddCoinsWalletAdministratorPolicy(
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
                    string? configuredRole = configuration[
                        "Identity:AdministratorRole"];

                    return string.Equals(
                               configuredRole,
                               "Admin",
                               StringComparison.Ordinal) &&
                           AdvertisementAdminPrincipalReadiness
                               .HasAuthenticatedAdminRole(
                                   context.User,
                                   configuredRole);
                });
            });
        });

        return services;
    }
}
