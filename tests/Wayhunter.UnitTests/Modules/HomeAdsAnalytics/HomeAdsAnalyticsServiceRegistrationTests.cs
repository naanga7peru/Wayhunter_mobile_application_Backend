
using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Wayhunter.Api.Extensions;
using Wayhunter.Application.Modules.HomeAdsAnalytics.Interfaces;
using Wayhunter.Application.Modules.HomeAdsAnalytics.Services;
using Wayhunter.Infrastructure.Persistence.Repositories.HomeAdsAnalytics;
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
            services.Where(x =>
                x.ServiceType == typeof(IAdvertisementRepository)));

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
            services.Where(x =>
                x.ServiceType == typeof(AdvertisementService)));

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
}
