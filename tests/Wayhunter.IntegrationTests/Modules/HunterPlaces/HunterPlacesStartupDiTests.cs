using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wayhunter.Api.Services.HunterPlaces;
using Wayhunter.Api.Services.IdentitySocial;
using Wayhunter.Application.Modules.HunterPlaces.Commands;
using Wayhunter.Application.Modules.HunterPlaces.Interfaces;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Wayhunter.Infrastructure.Persistence.Repositories.HunterPlaces;
using Xunit;

namespace Wayhunter.IntegrationTests.Modules.HunterPlaces;

public sealed class HunterPlacesStartupDiTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SqlDisabled_RegistersTrustedIdentityOnlyWhenAuthenticationIsEnabled(
        bool enableAuthentication)
    {
        using var factory = new StartupFactory(enableAuthentication, enableSql: false);
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;

        Assert.Equal(enableAuthentication,
            services.GetService<IHunterCaptureSessionIdentityResolver>() is not null);
        Assert.Null(services.GetService<IHunterCaptureSessionStore>());
        Assert.Null(services.GetService<HunterDraftCreationService>());
        Assert.Null(services.GetService<IHunterCaptureSessionRevocationStore>());
        Assert.Null(services.GetService<HunterConsumeAuthorizedCaptureSessionHandler>());
    }

    [Fact]
    public void SqlEnabledWithoutAuthentication_FailsAtStartup()
    {
        using var factory = new StartupFactory(enableAuthentication: false, enableSql: true);

        var exception = Assert.Throws<InvalidOperationException>(() => { _ = factory.Services; });
        Assert.Equal("M4 SQL persistence requires M1 shared authentication.", exception.Message);
    }

    [Fact]
    public void SqlEnabledWithAuthentication_ResolvesExistingRuntimeServices()
    {
        using var factory = new StartupFactory(enableAuthentication: true, enableSql: true);
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;

        Assert.Single(services.GetServices<WayhunterSharedDbContext>());
        Assert.Single(services.GetServices<IAuthenticatedSessionReader>());
        Assert.Single(services.GetServices<ITrustedIdentityAccountReader>());
        Assert.IsType<SqlHunterCaptureSessionStore>(
            Assert.Single(services.GetServices<IHunterCaptureSessionStore>()));
        Assert.IsType<SqlHunterCaptureSubmissionAccessReader>(
            services.GetRequiredService<IHunterCaptureSubmissionAccessReader>());
        Assert.IsType<SqlHunterSubmissionStatusTransitionStore>(
            services.GetRequiredService<IHunterSubmissionStatusTransitionStore>());
        Assert.Single(services.GetServices<HunterDraftCreationService>());
        Assert.Single(services.GetServices<HunterStartAuthorizedCaptureSessionHandler>());
        Assert.Single(services.GetServices<HunterConsumeAuthorizedCaptureSessionHandler>());
    }

    private sealed class StartupFactory(bool enableAuthentication, bool enableSql)
        : WebApplicationFactory<Program>
    {
        protected override IHost CreateHost(IHostBuilder builder)
        {
            builder.ConfigureHostConfiguration(configuration =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["HunterPlaces:EnableSqlPersistence"] = enableSql.ToString(),
                    ["Identity:EnableSharedAuthentication"] = enableAuthentication.ToString(),
                    ["CoinsWallet:EnableSqlRewardWriter"] = "false",
                    ["TripSafety:EnableSqlPersistence"] = "false",
                    ["TravelPlanning:EnableSqlPersistence"] = "false",
                    ["ConnectionStrings:WayhunterSharedDb"] =
                        "Server=127.0.0.1,1;Database=WayhunterM4StartupTest;Integrated Security=True;",
                    ["Authentication:Jwt:Issuer"] = "wayhunter-m4-test",
                    ["Authentication:Jwt:Audience"] = "wayhunter-m4-test",
                    ["Authentication:Jwt:SigningSecret"] = "test-only-m4-startup-signing-key-123456789"
                }));

            return base.CreateHost(builder);
        }
    }
}
