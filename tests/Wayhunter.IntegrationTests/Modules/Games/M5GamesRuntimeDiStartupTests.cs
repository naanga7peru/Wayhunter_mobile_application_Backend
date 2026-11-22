
using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wayhunter.Application.Modules.Games.Interfaces;
using Wayhunter.Application.Modules.Games.Services;
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

        public GamesStartupFactory(bool enableSql)
        {
            _enableSql = enableSql;
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
}
