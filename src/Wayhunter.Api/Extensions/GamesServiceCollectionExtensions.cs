
using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Wayhunter.Api.BackgroundServices;
using Wayhunter.Api.Services.Games;
using Wayhunter.Application.Modules.Games.Interfaces;
using Wayhunter.Application.Modules.Games.Services;
using Wayhunter.Infrastructure.Integrations.Games;
using Wayhunter.Infrastructure.Persistence.Repositories.Games;

namespace Wayhunter.Api.Extensions;

public static class GamesServiceCollectionExtensions
{
    private const string SqlPersistenceFlag =
        "Games:EnableSqlPersistence";
    private const string SqlAttemptWriterFlag =
        "Games:EnableSqlAttemptWriter";

    /// <summary>
    /// Registers the current M5 in-memory workflow store and
    /// the application services backed by its implemented contracts.
    /// SQL persistence remains limited to the CRUD contracts below.
    /// </summary>
    public static IServiceCollection AddGamesApplicationServices(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<InMemoryGameSubmissionStore>();

        services.TryAddSingleton<IGameRepository>(sp =>
            sp.GetRequiredService<InMemoryGameSubmissionStore>());
        services.TryAddSingleton<IGameDraftDeletionRepository>(sp =>
            sp.GetRequiredService<InMemoryGameSubmissionStore>());
        services.TryAddSingleton<IGameSubmissionStore>(sp =>
            sp.GetRequiredService<InMemoryGameSubmissionStore>());
        services.TryAddSingleton<IGameApprovalStore>(sp =>
            sp.GetRequiredService<InMemoryGameSubmissionStore>());
        services.TryAddSingleton<IGameModerationStore>(sp =>
            sp.GetRequiredService<InMemoryGameSubmissionStore>());
        // The in-memory workflow remains available to unit tests and draft workflows,
        // but must never acknowledge a mobile attempt as durably created.
        services.TryAddSingleton<IStartGameAttemptStore, UnavailableGameAttemptStore>();
        services.TryAddSingleton<ISubmitGameStageProofStore>(sp =>
            sp.GetRequiredService<InMemoryGameSubmissionStore>());
        services.TryAddSingleton<IGameStageProofDecisionStore>(sp =>
            sp.GetRequiredService<InMemoryGameSubmissionStore>());
        services.TryAddSingleton<IGamePendingStageProofQueryStore>(sp =>
            sp.GetRequiredService<InMemoryGameSubmissionStore>());
        services.TryAddSingleton<IAvailableGamesQueryStore>(sp =>
            sp.GetRequiredService<InMemoryGameSubmissionStore>());
        services.TryAddSingleton<IMyGamesQueryStore>(sp =>
            sp.GetRequiredService<InMemoryGameSubmissionStore>());
        services.TryAddSingleton<ICurrentGameStageQueryStore>(sp =>
            sp.GetRequiredService<InMemoryGameSubmissionStore>());
        services.TryAddSingleton<IExpiredGameArchiveStore>(sp =>
            sp.GetRequiredService<InMemoryGameSubmissionStore>());

        services.TryAddScoped<GameService>();
        services.TryAddScoped<GameDraftDeletionService>();
        services.TryAddScoped<GameSubmissionService>();
        services.TryAddScoped<GameApprovalService>();
        services.TryAddScoped<GameModerationService>();
        services.TryAddScoped<StartGameAttemptService>();
        services.TryAddScoped<GetPendingGameStageProofsService>();
        services.TryAddScoped<ArchiveExpiredGamesService>();
        services.TryAddScoped<GetMyGamesQueryService>();

        // Draft creation can proceed for text-only clues. Until
        // an approved private-media ownership provider exists,
        // media-bearing drafts are rejected by this fail-closed
        // default instead of trusting a client-supplied media ID.
        services.TryAddScoped<
            IGameClueMediaAccessVerifier,
            UnavailableGameClueMediaAccessVerifier>();
        services.TryAddScoped<GameClueMediaOwnershipService>();
        services.TryAddScoped<VerifiedGameDraftService>();

        return services;
    }

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
        services.RemoveAll<IExpiredGameArchiveStore>();

        services.AddScoped<IGameRepository>(
            sp => sp.GetRequiredService<SqlGameRepository>());

        services.AddScoped<IGameDraftDeletionRepository>(
            sp => sp.GetRequiredService<SqlGameRepository>());
        services.AddScoped<IExpiredGameArchiveStore>(
            sp => sp.GetRequiredService<SqlGameRepository>());

        return services;
    }

    // SQL is disabled unless explicitly enabled.
    // Preserve existing API behavior by default.
    public static IServiceCollection AddConfiguredGamesPersistence(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment? environment = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var enableSql =
            configuration.GetValue<bool>(SqlPersistenceFlag);

        if (enableSql)
        {
            services.AddGamesSqlPersistence(configuration);

            // Attempt writes are a separately opted-in local development
            // capability. Production and other environments remain fail-closed.
            if (environment?.IsDevelopment() == true &&
                configuration.GetValue<bool>(SqlAttemptWriterFlag))
            {
                services.AddScoped<SqlGameAttemptRepository>();
                services.RemoveAll<IStartGameAttemptStore>();
                services.AddScoped<IStartGameAttemptStore>(sp =>
                    sp.GetRequiredService<SqlGameAttemptRepository>());
            }

            if (configuration.GetValue<bool>(
                    "Games:EnableExpiryWorker"))
            {
                var intervalSeconds = Math.Clamp(
                    configuration.GetValue<int?>(
                        "Games:ExpiryWorkerIntervalSeconds") ?? 30,
                    5,
                    3600);
                var batchSize = Math.Clamp(
                    configuration.GetValue<int?>(
                        "Games:ExpiryArchiveBatchSize") ?? 100,
                    1,
                    1000);

                services.AddSingleton<IHostedService>(sp =>
                    new GameExpiryHostedService(
                        sp.GetRequiredService<IServiceScopeFactory>(),
                        sp.GetRequiredService<
                            Microsoft.Extensions.Logging.ILogger<GameExpiryHostedService>>(),
                        TimeSpan.FromSeconds(intervalSeconds),
                        batchSize));
            }
        }

        return services;
    }

    /// <summary>
    /// Registers only durable Games read providers. The in-memory application
    /// store is never selected for the mobile HTTP read routes.
    /// </summary>
    public static IServiceCollection AddGamesReadApiServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.RemoveAll<IAvailableGamesQueryStore>();
        services.RemoveAll<IPublicGameDetailsQueryStore>();
        services.RemoveAll<IMyGameAttemptsQueryStore>();
        services.RemoveAll<IMyGamesQueryStore>();

        if (configuration.GetValue<bool>(SqlPersistenceFlag))
        {
            services.TryAddScoped<SqlGamesReadRepository>();
            services.AddScoped<IAvailableGamesQueryStore>(sp =>
                sp.GetRequiredService<SqlGamesReadRepository>());
            services.AddScoped<IPublicGameDetailsQueryStore>(sp =>
                sp.GetRequiredService<SqlGamesReadRepository>());
            services.AddScoped<IMyGameAttemptsQueryStore>(sp =>
                sp.GetRequiredService<SqlGamesReadRepository>());
            services.AddScoped<IMyGamesQueryStore>(sp =>
                sp.GetRequiredService<SqlGamesReadRepository>());
        }
        else
        {
            services.TryAddSingleton<UnavailableGamesReadQueryStores>();
            services.AddSingleton<IAvailableGamesQueryStore>(sp =>
                sp.GetRequiredService<UnavailableGamesReadQueryStores>());
            services.AddSingleton<IPublicGameDetailsQueryStore>(sp =>
                sp.GetRequiredService<UnavailableGamesReadQueryStores>());
            services.AddSingleton<IMyGameAttemptsQueryStore>(sp =>
                sp.GetRequiredService<UnavailableGamesReadQueryStores>());
            services.AddSingleton<IMyGamesQueryStore>(sp =>
                sp.GetRequiredService<UnavailableGamesReadQueryStores>());
        }

        services.TryAddScoped<GetAvailableGamesQueryService>();
        services.TryAddScoped<GetPublicGameDetailsQueryService>();
        services.TryAddScoped<GetMyGameAttemptsQueryService>();
        return services;
    }
}
