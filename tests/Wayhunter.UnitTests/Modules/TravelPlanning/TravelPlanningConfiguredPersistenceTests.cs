
using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Wayhunter.Api.Extensions;
using Wayhunter.Application.Modules.TravelPlanning.Interfaces;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Wayhunter.Infrastructure.Persistence.Repositories.TravelPlanning;
using Xunit;

namespace Wayhunter.UnitTests.Modules.TravelPlanning;

public sealed class TravelPlanningConfiguredPersistenceTests
{
    private const string TestConnectionString =
        "Server=localhost;Database=Wayhunter_M2_Test;" +
        "Trusted_Connection=True;TrustServerCertificate=True;";

    private static IConfiguration CreateConfiguration(
        bool enableSql,
        string? connectionString = null)
    {
        var settings = new Dictionary<string, string?>
        {
            ["TravelPlanning:EnableSqlPersistence"] =
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
    public void SqlFlagDisabled_ShouldKeepInMemoryRepository()
    {
        var services = new ServiceCollection();

        services.AddSingleton<
            ITripPlanRepository,
            InMemoryTripPlanRepository>();

        services.AddConfiguredTravelPlanningPersistence(
            CreateConfiguration(enableSql: false));

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var repository = scope.ServiceProvider
            .GetRequiredService<ITripPlanRepository>();

        Assert.IsType<InMemoryTripPlanRepository>(repository);
    }

    [Fact]
    public void SqlFlagEnabled_ShouldUseEfRepository()
    {
        var services = new ServiceCollection();

        services.AddSingleton<
            ITripPlanRepository,
            InMemoryTripPlanRepository>();

        services.AddConfiguredTravelPlanningPersistence(
            CreateConfiguration(
                enableSql: true,
                connectionString: TestConnectionString));

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var repository = scope.ServiceProvider
            .GetRequiredService<ITripPlanRepository>();

        Assert.IsType<EfTripPlanRepository>(repository);

        var dbContext = scope.ServiceProvider
            .GetRequiredService<WayhunterSharedDbContext>();

        Assert.True(dbContext.Database.IsSqlServer());
    }

    [Fact]
    public void SqlFlagEnabled_WithoutConnection_ShouldThrow()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddConfiguredTravelPlanningPersistence(
                CreateConfiguration(enableSql: true)));

        Assert.Contains(
            "WayhunterSharedDb",
            exception.Message);
    }
}
