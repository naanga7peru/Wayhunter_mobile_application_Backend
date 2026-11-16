
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Wayhunter.Api.Extensions;
using Wayhunter.Application.Modules.TravelPlanning.Interfaces;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Wayhunter.Infrastructure.Persistence.Repositories.TravelPlanning;

namespace Wayhunter.UnitTests.Modules.TravelPlanning;

public sealed class TravelPlanningSqlServiceCollectionExtensionsTests
{
    private const string TestConnectionString =
        "Server=localhost;Database=Wayhunter_M2_Test;" +
        "Trusted_Connection=True;TrustServerCertificate=True;";

    private static IConfiguration CreateConfiguration(
        string? connectionString)
    {
        var settings = new Dictionary<string, string?>();

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
    public void AddSqlPersistence_NullServices_Throws()
    {
        IServiceCollection services = null!;

        Assert.Throws<ArgumentNullException>(() =>
            services.AddTravelPlanningSqlPersistence(
                CreateConfiguration(TestConnectionString)));
    }

    [Fact]
    public void AddSqlPersistence_NullConfiguration_Throws()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentNullException>(() =>
            services.AddTravelPlanningSqlPersistence(null!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AddSqlPersistence_MissingConnection_Throws(
        string? connectionString)
    {
        var services = new ServiceCollection();

        var configuration =
            CreateConfiguration(connectionString);

        Assert.Throws<InvalidOperationException>(() =>
            services.AddTravelPlanningSqlPersistence(
                configuration));
    }

    [Fact]
    public void AddSqlPersistence_RegistersSqlServerProvider()
    {
        var services = new ServiceCollection();

        services.AddTravelPlanningSqlPersistence(
            CreateConfiguration(TestConnectionString));

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<WayhunterSharedDbContext>();

        Assert.True(dbContext.Database.IsSqlServer());
    }

    [Fact]
    public void AddSqlPersistence_RegistersEfRepository()
    {
        var services = new ServiceCollection();

        services.AddTravelPlanningSqlPersistence(
            CreateConfiguration(TestConnectionString));

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var repository = scope.ServiceProvider
            .GetRequiredService<ITripPlanRepository>();

        Assert.IsType<EfTripPlanRepository>(repository);
    }

    [Fact]
    public void AddSqlPersistence_ReplacesInMemoryRepository()
    {
        var services = new ServiceCollection();

        // Simulate the existing default module registration.
        services.AddTravelPlanningModule();

        // Explicitly enable SQL persistence.
        services.AddTravelPlanningSqlPersistence(
            CreateConfiguration(TestConnectionString));

        var registrations = services
            .Where(descriptor =>
                descriptor.ServiceType ==
                typeof(ITripPlanRepository))
            .ToList();

        Assert.Single(registrations);

        Assert.Equal(
            typeof(EfTripPlanRepository),
            registrations[0].ImplementationType);

        Assert.Equal(
            ServiceLifetime.Scoped,
            registrations[0].Lifetime);
    }

    [Fact]
    public void AddSqlPersistence_DoesNotConnectToDatabase()
    {
        var services = new ServiceCollection();

        // This is a configuration-only test.
        // No SQL Server instance is required.
        services.AddTravelPlanningSqlPersistence(
            CreateConfiguration(TestConnectionString));

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<WayhunterSharedDbContext>();

        Assert.True(dbContext.Database.IsSqlServer());

        // Intentionally do not call:
        // Database.Migrate()
        // Database.EnsureCreated()
        // Database.CanConnect()
    }
}
