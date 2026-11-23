
using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wayhunter.Application.Modules.Games.Interfaces;
using Wayhunter.Application.Modules.Games.Services;
using Wayhunter.Api.Services.Games;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Wayhunter.Infrastructure.Persistence.Repositories.Games;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Wayhunter.IntegrationTests.Modules.Games;

public sealed class M5GamesRuntimeDiStartupTests
{
    private const string TestConnectionString =
        "Server=localhost;" +
        "Database=Wayhunter_M5_DI_StartupTest;" +
        "Trusted_Connection=True;" +
        "TrustServerCertificate=True;";

    private sealed class GamesStartupFactory
        : WebApplicationFactory<Program>
    {
        private readonly bool _enableSql;
        private readonly bool _enableAttemptWriter;
        private readonly string _environment;

        public GamesStartupFactory(
            bool enableSql,
            bool enableAttemptWriter = false,
            string environment = "Development")
        {
            _enableSql = enableSql;
            _enableAttemptWriter = enableAttemptWriter;
            _environment = environment;
        }

        protected override IHost CreateHost(
            IHostBuilder builder)
        {
            builder.ConfigureHostConfiguration(
                configuration =>
                {
                    configuration.AddInMemoryCollection(
                        new Dictionary<string, string?>
                        {
                            ["Games:EnableSqlPersistence"] =
                                _enableSql ? "true" : "false",
                            ["Games:EnableSqlAttemptWriter"] =
                                _enableAttemptWriter ? "true" : "false",
                            ["environment"] = _environment,

                            ["Identity:EnableSharedAuthentication"] =
                                "false",

                            ["TravelPlanning:EnableSqlPersistence"] =
                                "false",

                            ["ConnectionStrings:WayhunterSharedDb"] =
                                TestConnectionString
                        });
                });

            return base.CreateHost(builder);
        }
    }

    [Fact]
    public void ActualProgram_SqlDisabled_UsesInMemoryStoreWithoutSqlRepository()
    {
        using var factory = new GamesStartupFactory(false);
        using var scope = factory.Services.CreateScope();

        var gameRepository = scope.ServiceProvider
            .GetService<IGameRepository>();

        var deletionRepository = scope.ServiceProvider
            .GetService<IGameDraftDeletionRepository>();
        var publicGamesQuery = scope.ServiceProvider
            .GetRequiredService<IAvailableGamesQueryStore>();

        Assert.IsType<InMemoryGameSubmissionStore>(gameRepository);
        Assert.Same(gameRepository, deletionRepository);
        Assert.IsType<UnavailableGamesReadQueryStores>(publicGamesQuery);
        Assert.Null(scope.ServiceProvider.GetService<SqlGameRepository>());
        Assert.IsType<UnavailableGameAttemptStore>(
            scope.ServiceProvider.GetRequiredService<IStartGameAttemptStore>());
    }

    [Fact]
    public void ActualProgram_SqlEnabled_ResolvesGameSqlRepository()
    {
        using var factory = new GamesStartupFactory(true);
        using var scope = factory.Services.CreateScope();

        var gameRepository = scope.ServiceProvider
            .GetRequiredService<IGameRepository>();

        var deletionRepository = scope.ServiceProvider
            .GetRequiredService<IGameDraftDeletionRepository>();

        var sqlRepository = scope.ServiceProvider
            .GetRequiredService<SqlGameRepository>();
        var publicGamesQuery = scope.ServiceProvider
            .GetRequiredService<IAvailableGamesQueryStore>();

        Assert.Same(sqlRepository, gameRepository);
        Assert.Same(sqlRepository, deletionRepository);
        Assert.IsType<SqlGamesReadRepository>(publicGamesQuery);
    }

    [Fact]
    public void ActualProgram_SqlEnabled_UsesSharedSqlContext()
    {
        using var factory = new GamesStartupFactory(true);
        using var scope = factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<WayhunterSharedDbContext>();

        Assert.True(db.Database.IsSqlServer());
    }

    [Fact]
    public void ActualProgram_DevelopmentAttemptWriterOptIn_ResolvesSqlStore()
    {
        using var factory = new GamesStartupFactory(
            enableSql: true,
            enableAttemptWriter: true);
        using var scope = factory.Services.CreateScope();

        Assert.IsType<SqlGameAttemptRepository>(
            scope.ServiceProvider.GetRequiredService<IStartGameAttemptStore>());
    }

    [Fact]
    public void ActualProgram_NonDevelopmentAttemptWriterOptIn_RemainsUnavailable()
    {
        using var factory = new GamesStartupFactory(
            enableSql: true,
            enableAttemptWriter: true,
            environment: Environments.Production);
        using var scope = factory.Services.CreateScope();

        Assert.IsType<UnavailableGameAttemptStore>(
            scope.ServiceProvider.GetRequiredService<IStartGameAttemptStore>());
    }
}
