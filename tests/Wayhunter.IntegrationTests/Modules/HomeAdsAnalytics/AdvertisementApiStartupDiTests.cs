
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Hosting;
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
        private readonly bool _enabled;

        public M7StartupFactory(bool enabled)
        {
            _enabled = enabled;
        }

        protected override IHost CreateHost(
            IHostBuilder builder)
        {
            // Apply settings before Program.cs reads configuration.
            builder.ConfigureHostConfiguration(configuration =>
            {
                configuration.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["Identity:EnableSharedAuthentication"] =
                            _enabled ? "true" : "false",

                        // Test-only SQL connection string.
                        // No database operations are performed.
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

    [Fact]
    public void SharedAuthenticationDisabled_M7SqlServicesAbsent()
    {
        using var factory = new M7StartupFactory(
            enabled: false);

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
    public void SharedAuthenticationEnabled_M7SqlServicesResolve()
    {
        using var factory = new M7StartupFactory(
            enabled: true);

        using var scope = factory.Services.CreateScope();

        var provider = scope.ServiceProvider;

        Assert.IsType<AdvertisementRepository>(
            provider.GetRequiredService<IAdvertisementRepository>());

        Assert.IsType<AdvertisementRepository>(
            provider.GetRequiredService<IAdvertisementWriteRepository>());

        Assert.NotNull(
            provider.GetRequiredService<AdvertisementService>());

        var dbContext =
            provider.GetRequiredService<WayhunterSharedDbContext>();

        Assert.True(dbContext.Database.IsSqlServer());
    }

    [Fact]
    public void SharedAuthenticationEnabled_ReusesOneSharedDbContext()
    {
        using var factory = new M7StartupFactory(
            enabled: true);

        using var scope = factory.Services.CreateScope();

        var provider = scope.ServiceProvider;

        var contexts = provider
            .GetServices<WayhunterSharedDbContext>()
            .ToArray();

        Assert.Single(contexts);

        var resolvedContext =
            provider.GetRequiredService<WayhunterSharedDbContext>();

        Assert.Same(contexts[0], resolvedContext);

        Assert.True(resolvedContext.Database.IsSqlServer());
    }
}
