
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Wayhunter.Api.Extensions;
using Wayhunter.Application.Modules.HomeAdsAnalytics.Interfaces;
using Wayhunter.Application.Modules.HomeAdsAnalytics.Services;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Wayhunter.Infrastructure.Persistence.Repositories.HomeAdsAnalytics;
using Xunit;

namespace Wayhunter.IntegrationTests.Modules.HomeAdsAnalytics;

public sealed class AdvertisementSharedAuthenticationDiTests
{
    private static IConfiguration CreateTestConfiguration()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:WayhunterSharedDb"] =
                        "Server=localhost,1433;" +
                        "Database=Wayhunter_M7_DI_Only;" +
                        "User Id=di_test;" +
                        "Password=not-a-real-password;" +
                        "Encrypt=True;" +
                        "TrustServerCertificate=True"
                })
            .Build();
    }

    [Fact]
    public void DefaultApiStartup_M7SqlServicesRemainDisabled()
    {
        using var factory =
            new WebApplicationFactory<Program>();

        using var scope = factory.Services.CreateScope();

        var provider = scope.ServiceProvider;

        Assert.Null(
            provider.GetService<IAdvertisementRepository>());

        Assert.Null(
            provider.GetService<IAdvertisementWriteRepository>());

        Assert.Null(
            provider.GetService<AdvertisementService>());

        Assert.Null(
            provider.GetService<WayhunterSharedDbContext>());
    }

    [Fact]
    public void M7SqlRegistration_ResolvesAllRequiredServices()
    {
        var services = new ServiceCollection();

        var configuration = CreateTestConfiguration();

        services.AddHomeAdsAnalyticsSqlPersistence(
            configuration);

        using var provider = services.BuildServiceProvider(
            validateScopes: true);

        using var scope = provider.CreateScope();

        var scoped = scope.ServiceProvider;

        Assert.IsType<AdvertisementRepository>(
            scoped.GetRequiredService<IAdvertisementRepository>());

        Assert.IsType<AdvertisementRepository>(
            scoped.GetRequiredService<IAdvertisementWriteRepository>());

        Assert.NotNull(
            scoped.GetRequiredService<AdvertisementService>());

        var dbContext =
            scoped.GetRequiredService<WayhunterSharedDbContext>();

        Assert.True(dbContext.Database.IsSqlServer());
    }

    [Fact]
    public void M7SqlRegistration_ReusesOneSharedDbContext()
    {
        var services = new ServiceCollection();

        var configuration = CreateTestConfiguration();

        // Simulate M1 registering shared persistence first.
        services.AddWayhunterSharedPersistence(
            configuration);

        // M7 must reuse the existing shared DbContext.
        services.AddHomeAdsAnalyticsSqlPersistence(
            configuration);

        Assert.Single(
            services,
            descriptor =>
                descriptor.ServiceType ==
                typeof(DbContextOptions<WayhunterSharedDbContext>));

        using var provider = services.BuildServiceProvider(
            validateScopes: true);

        using var scope = provider.CreateScope();

        var scoped = scope.ServiceProvider;

        var contexts = scoped
            .GetServices<WayhunterSharedDbContext>()
            .ToArray();

        Assert.Single(contexts);

        Assert.Same(
            contexts[0],
            scoped.GetRequiredService<WayhunterSharedDbContext>());

        Assert.True(
            contexts[0].Database.IsSqlServer());
    }
}
