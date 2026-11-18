
using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Wayhunter.Api.Extensions;
using Wayhunter.Application.Modules.Games.Interfaces;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Wayhunter.Infrastructure.Persistence.Repositories.Games;
using Xunit;

namespace Wayhunter.UnitTests.Modules.Games;

public sealed class GamesServiceCollectionExtensionsTests
{
    private const string TestConnectionString =
        "Server=localhost;Database=Wayhunter_M5_DI_Test;" +
        "Trusted_Connection=True;TrustServerCertificate=True;";

    private static IConfiguration CreateConfiguration(
        bool enableSql,
        string? connectionString = TestConnectionString)
    {
        var settings = new Dictionary<string, string?>
        {
            ["Games:EnableSqlPersistence"] =
                enableSql.ToString()
        };

        if (connectionString is not null)
        {
            settings["ConnectionStrings:WayhunterSharedDb"] =
                connectionString;
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();
    }

    [Fact]
    public void SqlDisabled_DoesNotRegisterSqlRepository()
    {
        var services = new ServiceCollection();

        services.AddConfiguredGamesPersistence(
            CreateConfiguration(false));

        Assert.DoesNotContain(
            services,
            x => x.ServiceType == typeof(SqlGameRepository));

        Assert.DoesNotContain(
            services,
            x => x.ServiceType ==
                typeof(WayhunterSharedDbContext));
    }

    [Fact]
    public void SqlEnabled_RegistersSqlRepository()
    {
        var services = new ServiceCollection();

        services.AddConfiguredGamesPersistence(
            CreateConfiguration(true));

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var repository = scope.ServiceProvider
            .GetRequiredService<IGameRepository>();

        Assert.IsType<SqlGameRepository>(repository);
    }

    [Fact]
    public void SqlEnabled_BothInterfacesUseSameScopedInstance()
    {
        var services = new ServiceCollection();

        services.AddConfiguredGamesPersistence(
            CreateConfiguration(true));

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var gameRepository = scope.ServiceProvider
            .GetRequiredService<IGameRepository>();

        var deletionRepository = scope.ServiceProvider
            .GetRequiredService<IGameDraftDeletionRepository>();

        Assert.Same(gameRepository, deletionRepository);
    }

    [Fact]
    public void SqlEnabled_UsesSharedSqlServerDbContext()
    {
        var services = new ServiceCollection();

        services.AddConfiguredGamesPersistence(
            CreateConfiguration(true));

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<WayhunterSharedDbContext>();

        Assert.True(db.Database.IsSqlServer());
    }

    [Fact]
    public void SqlEnabled_ReplacesExistingCrudRegistrations()
    {
        var services = new ServiceCollection();

        services.AddScoped<
            IGameRepository,
            InMemoryGameSubmissionStore>();

        services.AddScoped<
            IGameDraftDeletionRepository,
            InMemoryGameSubmissionStore>();

        services.AddConfiguredGamesPersistence(
            CreateConfiguration(true));

        var crudRegistration = Assert.Single(
            services,
            x => x.ServiceType == typeof(IGameRepository));

        var deletionRegistration = Assert.Single(
            services,
            x => x.ServiceType ==
                typeof(IGameDraftDeletionRepository));

        Assert.Equal(
            ServiceLifetime.Scoped,
            crudRegistration.Lifetime);

        Assert.Equal(
            ServiceLifetime.Scoped,
            deletionRegistration.Lifetime);

        Assert.NotNull(crudRegistration.ImplementationFactory);
        Assert.NotNull(deletionRegistration.ImplementationFactory);
    }

    [Fact]
    public void SqlEnabled_DoesNotReplaceApprovalWorkflow()
    {
        var services = new ServiceCollection();

        services.AddSingleton<
            IGameApprovalStore,
            InMemoryGameSubmissionStore>();

        services.AddConfiguredGamesPersistence(
            CreateConfiguration(true));

        var registration = Assert.Single(
            services,
            x => x.ServiceType == typeof(IGameApprovalStore));

        Assert.Equal(
            typeof(InMemoryGameSubmissionStore),
            registration.ImplementationType);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SqlEnabled_MissingConnection_Throws(
        string? connectionString)
    {
        var services = new ServiceCollection();

        Assert.Throws<InvalidOperationException>(
            () => services.AddConfiguredGamesPersistence(
                CreateConfiguration(true, connectionString)));
    }

    [Fact]
    public void SqlDisabled_DoesNotRequireConnection()
    {
        var services = new ServiceCollection();

        services.AddConfiguredGamesPersistence(
            CreateConfiguration(false, null));

        Assert.DoesNotContain(
            services,
            x => x.ServiceType == typeof(SqlGameRepository));
    }
}
