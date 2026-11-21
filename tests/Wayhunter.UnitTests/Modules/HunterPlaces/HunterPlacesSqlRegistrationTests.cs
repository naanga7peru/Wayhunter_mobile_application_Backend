
using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Wayhunter.Api.Extensions;
using Wayhunter.Application.Modules.HunterPlaces.Commands;
using Wayhunter.Application.Modules.HunterPlaces.Interfaces;
using Wayhunter.Application.Modules.HunterPlaces.Services;
using Wayhunter.Infrastructure.Persistence.Repositories.HunterPlaces;
using Xunit;

namespace Wayhunter.UnitTests.Modules.HunterPlaces;

public sealed class HunterPlacesSqlRegistrationTests
{
    private static IConfiguration CreateConfiguration(
        bool enableSql,
        bool enableAuthentication)
    {
        var settings = new Dictionary<string, string?>
{
    ["HunterPlaces:EnableSqlPersistence"] =
        enableSql.ToString(),

    ["Identity:EnableSharedAuthentication"] =
        enableAuthentication.ToString(),

    // Dummy connection string for DI registration tests only.
    // No SQL Server connection is opened.
    ["ConnectionStrings:WayhunterSharedDb"] =
        "Server=localhost;Database=WayhunterM4DiTest;" +
        "Integrated Security=True;TrustServerCertificate=True;"
};

        return new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();
    }

    [Fact]
    public void DisabledSqlFlag_ShouldNotRegisterM4SqlServices()
    {
        var services = new ServiceCollection();
        var configuration = CreateConfiguration(
            enableSql: false,
            enableAuthentication: true);

        services.AddConfiguredHunterPlacesPersistence(
            configuration);

        Assert.DoesNotContain(
            services,
            descriptor => descriptor.ServiceType ==
                typeof(IHunterCaptureSessionStore));

        Assert.DoesNotContain(
            services,
            descriptor => descriptor.ServiceType ==
                typeof(IHunterCaptureSubmissionAccessReader));

        Assert.DoesNotContain(
            services,
            descriptor => descriptor.ServiceType ==
                typeof(HunterStartAuthorizedCaptureSessionHandler));
    }

    [Fact]
    public void EnabledSqlWithoutAuthentication_ShouldThrow()
    {
        var services = new ServiceCollection();
        var configuration = CreateConfiguration(
            enableSql: true,
            enableAuthentication: false);

        Assert.Throws<InvalidOperationException>(() =>
            services.AddConfiguredHunterPlacesPersistence(
                configuration));
    }

    [Fact]
    public void EnabledSqlWithAuthentication_ShouldRegisterRepositories()
    {
        var services = new ServiceCollection();
        var configuration = CreateConfiguration(
            enableSql: true,
            enableAuthentication: true);

        services.AddConfiguredHunterPlacesPersistence(
            configuration);

        Assert.Single(
            services,
            descriptor => descriptor.ServiceType ==
                typeof(IHunterCaptureSessionStore));

        Assert.Single(
            services,
            descriptor => descriptor.ServiceType ==
                typeof(IHunterCaptureSessionRevocationStore));

        Assert.Single(
            services,
            descriptor => descriptor.ServiceType ==
                typeof(IHunterCaptureSubmissionAccessReader));

        Assert.Single(
            services,
            descriptor => descriptor.ServiceType ==
                typeof(IHunterSubmissionStatusTransitionStore));
    }

    [Fact]
    public void EnabledSql_ShouldRegisterApplicationHandlers()
    {
        var services = new ServiceCollection();
        var configuration = CreateConfiguration(
            enableSql: true,
            enableAuthentication: true);

        services.AddConfiguredHunterPlacesPersistence(
            configuration);

        Assert.Single(
            services,
            descriptor => descriptor.ServiceType ==
                typeof(HunterCaptureSessionService));

        Assert.Single(
            services,
            descriptor => descriptor.ServiceType ==
                typeof(HunterStartCaptureSessionHandler));

        Assert.Single(
            services,
            descriptor => descriptor.ServiceType ==
                typeof(HunterStartAuthorizedCaptureSessionHandler));

        Assert.Single(
            services,
            descriptor => descriptor.ServiceType ==
                typeof(HunterCaptureSubmissionOwnershipService));
    }

    [Fact]
    public void BothCaptureInterfaces_ShouldUseSameScopedImplementation()
    {
        var services = new ServiceCollection();
        var configuration = CreateConfiguration(
            enableSql: true,
            enableAuthentication: true);

        services.AddConfiguredHunterPlacesPersistence(
            configuration);

        var captureRegistration = Assert.Single(
            services,
            descriptor => descriptor.ServiceType ==
                typeof(IHunterCaptureSessionStore));

        var revocationRegistration = Assert.Single(
            services,
            descriptor => descriptor.ServiceType ==
                typeof(IHunterCaptureSessionRevocationStore));

        Assert.Equal(
            ServiceLifetime.Scoped,
            captureRegistration.Lifetime);

        Assert.Equal(
            ServiceLifetime.Scoped,
            revocationRegistration.Lifetime);

        Assert.Single(
            services,
            descriptor => descriptor.ServiceType ==
                typeof(SqlHunterCaptureSessionStore));

        using var provider = services.BuildServiceProvider();
        using var firstScope = provider.CreateScope();
        using var secondScope = provider.CreateScope();

        var firstStore = firstScope.ServiceProvider
            .GetRequiredService<IHunterCaptureSessionStore>();
        var secondStore = secondScope.ServiceProvider
            .GetRequiredService<IHunterCaptureSessionStore>();

        Assert.IsType<SqlHunterCaptureSessionStore>(firstStore);
        Assert.Same(firstStore, firstScope.ServiceProvider
            .GetRequiredService<IHunterCaptureSessionRevocationStore>());
        Assert.Same(secondStore, secondScope.ServiceProvider
            .GetRequiredService<IHunterCaptureSessionRevocationStore>());
        Assert.NotSame(firstStore, secondStore);
    }
}
