
using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Wayhunter.Infrastructure.Persistence.DbContexts;

namespace Wayhunter.Api.Extensions;

public static class SharedPersistenceServiceCollectionExtensions
{
    private const string ConnectionStringName = "WayhunterSharedDb";

    public static IServiceCollection AddWayhunterSharedPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // Ensure only one authoritative DbContext registration.
        var alreadyRegistered = services.Any(
            descriptor =>
                descriptor.ServiceType ==
                typeof(DbContextOptions<WayhunterSharedDbContext>));

        if (alreadyRegistered)
        {
            return services;
        }

        var connectionString =
            configuration.GetConnectionString(ConnectionStringName);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"SQL Server connection string '{ConnectionStringName}' " +
                "is missing.");
        }

        // The factory also exposes WayhunterSharedDbContext as a scoped
        // service, while supporting SQL stores that create independent
        // contexts (notably the M3 check-in and notification workflows).
        services.AddDbContextFactory<WayhunterSharedDbContext>(
            options =>
                options.UseSqlServer(
                    connectionString,
                    sqlOptions =>
                        sqlOptions.EnableRetryOnFailure(
                            maxRetryCount: 3)));

        return services;
    }
}
