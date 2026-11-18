
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wayhunter.Application.Modules.HomeAdsAnalytics.Interfaces;
using Wayhunter.Application.Modules.HomeAdsAnalytics.Services;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Wayhunter.Infrastructure.Persistence.Repositories.HomeAdsAnalytics;
using Xunit;

namespace Wayhunter.IntegrationTests.Modules.HomeAdsAnalytics;

public sealed class AdvertisementApiStartupDiTests
{
    private sealed class M7StartupFactory
        : WebApplicationFactory<Program>
    {
        private readonly bool _sharedAuthenticationEnabled;
        private readonly bool _advertisementSqlEnabled;

        public M7StartupFactory(
            bool sharedAuthenticationEnabled,
            bool advertisementSqlEnabled)
        {
            _sharedAuthenticationEnabled =
                sharedAuthenticationEnabled;

            _advertisementSqlEnabled =
                advertisementSqlEnabled;
        }

        protected override IHost CreateHost(
            IHostBuilder builder)
        {
            // Configuration must be available before
            // Program.cs evaluates the feature flags.
            builder.ConfigureHostConfiguration(configuration =>
            {
                configuration.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["Identity:EnableSharedAuthentication"] =
                            _sharedAuthenticationEnabled
                                ? "true"
                                : "false",

                        ["Advertisements:EnableSqlPersistence"] =
                            _advertisementSqlEnabled
                                ? "true"
                                : "false",

                        // Test-only connection string.
                        // No database connection is opened.
                        ["ConnectionStrings:WayhunterSharedDb"] =
                            "Server=localhost;" +
                            "Database=Wayhunter_M7_Startup_DI;" +
                            "Trusted_Connection=True;" +
                            "TrustServerCertificate=True",

                        ["Authentication:Jwt:Issuer"] =
                            "wayhunter-m7-startup-test",

                        ["Authentication:Jwt:Audience"] =
                            "wayhunter-mobile-test",

                        ["Authentication:Jwt:SigningSecret"] =
                            "test-only-m7-startup-secret-123456789"
                    });
            });

            return base.CreateHost(builder);
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void M7SqlDisabled_AdvertisementServicesAbsent(
        bool sharedAuthenticationEnabled,
        bool advertisementSqlEnabled)
    {
        using var factory = new M7StartupFactory(
            sharedAuthenticationEnabled,
            advertisementSqlEnabled);

        using var scope = factory.Services.CreateScope();

        var provider = scope.ServiceProvider;

        Assert.Null(
            provider.GetService<IAdvertisementRepository>());

        Assert.Null(
            provider.GetService<IAdvertisementWriteRepository>());

        Assert.Null(
            provider.GetService<AdvertisementService>());

        // M1 may still register the shared DbContext.
        // Therefore, its absence is not required here.
    }

    [Fact]
    public void BothFlagsEnabled_M7SqlServicesResolve()
    {
        using var factory = new M7StartupFactory(
            sharedAuthenticationEnabled: true,
            advertisementSqlEnabled: true);

        using var scope = factory.Services.CreateScope();

        var provider = scope.ServiceProvider;

        Assert.IsType<AdvertisementRepository>(
            provider.GetRequiredService<
                IAdvertisementRepository>());

        Assert.IsType<AdvertisementRepository>(
            provider.GetRequiredService<
                IAdvertisementWriteRepository>());

        Assert.NotNull(
            provider.GetRequiredService<
                AdvertisementService>());

        var dbContext =
            provider.GetRequiredService<
                WayhunterSharedDbContext>();

        Assert.True(dbContext.Database.IsSqlServer());
    }

    [Fact]
    public void BothFlagsEnabled_ReusesOneSharedDbContext()
    {
        using var factory = new M7StartupFactory(
            sharedAuthenticationEnabled: true,
            advertisementSqlEnabled: true);

        using var scope = factory.Services.CreateScope();

        var provider = scope.ServiceProvider;

        var contexts = provider
            .GetServices<WayhunterSharedDbContext>()
            .ToArray();

        Assert.Single(contexts);

        var resolvedContext =
            provider.GetRequiredService<
                WayhunterSharedDbContext>();

        Assert.Same(contexts[0], resolvedContext);

        Assert.True(
            resolvedContext.Database.IsSqlServer());
    }

    [Fact]
    public void M1Enabled_M7Disabled_SharedDbContextRemainsAvailable()
    {
        using var factory = new M7StartupFactory(
            sharedAuthenticationEnabled: true,
            advertisementSqlEnabled: false);

        using var scope = factory.Services.CreateScope();

        var provider = scope.ServiceProvider;

        var context =
            provider.GetRequiredService<
                WayhunterSharedDbContext>();

        Assert.True(context.Database.IsSqlServer());

        Assert.Null(
            provider.GetService<IAdvertisementRepository>());

        Assert.Null(
            provider.GetService<IAdvertisementWriteRepository>());
    }
}
