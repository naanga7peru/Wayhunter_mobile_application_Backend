
using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wayhunter.Application.Modules.Games.Interfaces;
using Wayhunter.Infrastructure.Persistence.Repositories.Games;

namespace Wayhunter.Api.Extensions;

public static class GamesServiceCollectionExtensions
{
    private const string SqlPersistenceFlag =
        "Games:EnableSqlPersistence";

    // Register only the M5 SQL CRUD repository contracts.
    // Other Game workflows are not SQL-ready yet.
    public static IServiceCollection AddGamesSqlPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // Reuse the existing shared SQL DbContext.
        // This validates the connection string but
        // does not connect to or migrate the database.
        services.AddWayhunterSharedPersistence(
            configuration);

        // One SQL repository instance per DI scope.
        services.TryAddScoped<SqlGameRepository>();

        // Switch only the repository contracts that
        // SqlGameRepository actually implements.
        services.RemoveAll<IGameRepository>();
        services.RemoveAll<IGameDraftDeletionRepository>();

        services.AddScoped<IGameRepository>(
            sp => sp.GetRequiredService<SqlGameRepository>());

        services.AddScoped<IGameDraftDeletionRepository>(
            sp => sp.GetRequiredService<SqlGameRepository>());

        return services;
    }

    // SQL is disabled unless explicitly enabled.
    // Preserve existing API behavior by default.
    public static IServiceCollection AddConfiguredGamesPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var enableSql =
            configuration.GetValue<bool>(SqlPersistenceFlag);

        if (enableSql)
        {
            services.AddGamesSqlPersistence(configuration);
        }

        return services;
    }
}
