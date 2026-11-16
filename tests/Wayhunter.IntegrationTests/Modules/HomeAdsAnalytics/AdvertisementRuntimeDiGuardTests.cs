
using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wayhunter.Application.Modules.HomeAdsAnalytics.Interfaces;
using Wayhunter.Application.Modules.HomeAdsAnalytics.Services;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Xunit;

namespace Wayhunter.IntegrationTests.Modules.HomeAdsAnalytics;

public sealed class AdvertisementRuntimeDiGuardTests
{
    // M7-MS02-T03:
    // Before shared SQL registration is approved,
    // the production API must not activate an
    // incomplete Advertisement service graph.

    [Fact]
    public void ApiStartup_ShouldNotRegisterAdvertisementRepository()
    {
        using var factory =
            new WebApplicationFactory<Program>();

        var registrations =
            factory.Services.GetService<IServiceCollection>();

        // IServiceCollection is not necessarily
        // exposed by the built ServiceProvider.
        // Runtime resolution is checked directly.
        using var scope =
            factory.Services.CreateScope();

        var repository = scope.ServiceProvider
            .GetService<IAdvertisementRepository>();

        Assert.Null(repository);
    }

    [Fact]
    public void ApiStartup_ShouldNotRegisterAdvertisementService()
    {
        using var factory =
            new WebApplicationFactory<Program>();

        using var scope =
            factory.Services.CreateScope();

        var service = scope.ServiceProvider
            .GetService<AdvertisementService>();

        Assert.Null(service);
    }

    [Fact]
    public void ApiStartup_ShouldNotRegisterAdvertisementDbContext()
    {
        using var factory =
            new WebApplicationFactory<Program>();

        using var scope =
            factory.Services.CreateScope();

        var dbContext = scope.ServiceProvider
            .GetService<HomeAdsAnalyticsDbContext>();

        Assert.Null(dbContext);
    }
}
