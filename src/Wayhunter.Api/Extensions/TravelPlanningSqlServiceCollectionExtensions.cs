
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wayhunter.Application.Modules.TravelPlanning.Interfaces;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Wayhunter.Infrastructure.Persistence.Repositories.TravelPlanning;

namespace Wayhunter.Api.Extensions;

/// <summary>
/// Optional SQL Server persistence configuration for
/// WAYHUNTER M2 Travel Planning.
///
/// SQL persistence is not enabled automatically.
/// The existing InMemory repository remains the default.
///
/// No migrations or database updates are executed here.
/// </summary>
public static class TravelPlanningSqlServiceCollectionExtensions
{
    private const string ConnectionStringName =
        "WayhunterSharedDb";

    public static IServiceCollection AddTravelPlanningSqlPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // Step 1: Read shared SQL Server connection string.
        var connectionString =
            configuration.GetConnectionString(ConnectionStringName);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"SQL Server connection string '{ConnectionStringName}' " +
                "is missing. Configure it before enabling SQL persistence.");
        }

        // Step 2: Register the shared EF Core DbContext.
        // Avoid registering the same DbContext more than once.
        services.AddDbContext<WayhunterSharedDbContext>(
            options =>
            {
                options.UseSqlServer(
                    connectionString,
                    sqlOptions =>
                    {
                        sqlOptions.EnableRetryOnFailure(
                            maxRetryCount: 3);
                    });
            });

        // Step 3: Replace the temporary InMemory repository
        // with the SQL-backed repository when explicitly enabled.
        services.Replace(
            ServiceDescriptor.Scoped<
                ITripPlanRepository,
                EfTripPlanRepository>());

        return services;
    }
}
