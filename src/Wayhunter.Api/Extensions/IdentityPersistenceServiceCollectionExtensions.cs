
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Wayhunter.Api.Services.IdentitySocial;
using Wayhunter.Infrastructure.Persistence.DbContexts;

namespace Wayhunter.Api.Extensions;

public static class IdentityPersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddWayhunterIdentityPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString =
            configuration.GetConnectionString("WayhunterIdentity");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "WayhunterIdentity database connection is not configured.");
        }

        services.AddDbContext<IdentityDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddScoped<
            IAuthenticatedSessionReader,
            SqlAuthenticatedSessionReader>();

        services.AddScoped<
            IIdentitySessionRevocationStore,
            SqlIdentitySessionRevocationStore>();

        services.AddScoped<SessionLogoutService>();

        return services;
    }
}
