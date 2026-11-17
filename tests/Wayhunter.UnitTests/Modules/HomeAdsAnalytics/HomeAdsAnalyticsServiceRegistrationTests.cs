
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Wayhunter.Api.Extensions;
using Wayhunter.Application.Modules.HomeAdsAnalytics.Interfaces;
using Wayhunter.Application.Modules.HomeAdsAnalytics.Services;
using Wayhunter.Infrastructure.Persistence.Repositories.HomeAdsAnalytics;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Xunit;

namespace Wayhunter.UnitTests.Modules.HomeAdsAnalytics;

public sealed class HomeAdsAnalyticsServiceRegistrationTests
{
    [Fact]
    public void Foundation_ShouldRegisterAdvertisementRepositoryAsScoped()
    {
        var services = new ServiceCollection();

        services.AddHomeAdsAnalyticsFoundation();

        var registration = Assert.Single(
    services,
    x => x.ServiceType == typeof(IAdvertisementRepository));
        Assert.Equal(
            ServiceLifetime.Scoped,
            registration.Lifetime);

        Assert.Equal(
            typeof(AdvertisementRepository),
            registration.ImplementationType);
    }

    [Fact]
    public void Foundation_ShouldRegisterAdvertisementServiceAsScoped()
    {
        var services = new ServiceCollection();

        services.AddHomeAdsAnalyticsFoundation();

        var registration = Assert.Single(
    services,
    x => x.ServiceType == typeof(AdvertisementService));

        Assert.Equal(
            ServiceLifetime.Scoped,
            registration.Lifetime);
    }

    [Fact]
    public void Foundation_ShouldRejectNullServiceCollection()
    {
        IServiceCollection services = null!;

        Assert.Throws<ArgumentNullException>(
            () => services.AddHomeAdsAnalyticsFoundation());
    }

    [Fact]
    public void Foundation_ShouldNotRegisterSqlDbContext()
    {
        var services = new ServiceCollection();

        services.AddHomeAdsAnalyticsFoundation();

        Assert.DoesNotContain(
            services,
            x => x.ServiceType.Name.Contains(
                "HomeAdsAnalyticsDbContext",
                StringComparison.Ordinal));
    }

    [Fact]
    public void SqlPersistence_ShouldRegisterOneSharedContextAndResolveRepository()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:WayhunterSharedDb"] =
                    "Server=localhost;Database=WayhunterM7DiOnly;Trusted_Connection=True;"
            })
            .Build();

        var services = new ServiceCollection();

        services.AddHomeAdsAnalyticsSqlPersistence(configuration);
        services.AddHomeAdsAnalyticsSqlPersistence(configuration);

        Assert.Single(
            services,
            descriptor => descriptor.ServiceType ==
                typeof(DbContextOptions<WayhunterSharedDbContext>));

        Assert.Single(
            services,
            descriptor => descriptor.ServiceType ==
                typeof(IAdvertisementRepository));

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.IsType<AdvertisementRepository>(
            scope.ServiceProvider
                .GetRequiredService<IAdvertisementRepository>());

        Assert.NotNull(
            scope.ServiceProvider
                .GetRequiredService<WayhunterSharedDbContext>());
    }
}
