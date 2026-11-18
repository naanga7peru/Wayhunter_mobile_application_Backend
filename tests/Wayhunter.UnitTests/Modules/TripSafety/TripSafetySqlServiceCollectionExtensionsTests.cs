
using System;
using System.Collections.Generic;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Wayhunter.Api.Extensions;

using Wayhunter.Application.Modules.TripSafety.Interfaces;

using Wayhunter.Infrastructure.Persistence.DbContexts;
using Wayhunter.Infrastructure.Persistence.Repositories.TripSafety;

using Xunit;

namespace Wayhunter.UnitTests.Modules.TripSafety;

public sealed class TripSafetySqlServiceCollectionExtensionsTests
{
    private const string TestConnectionString =
        "Server=localhost;" +
        "Database=Wayhunter_M3_DI_Test;" +
        "Trusted_Connection=True;" +
        "TrustServerCertificate=True;";

    private static IConfiguration CreateConfiguration(
        bool enabled,
        string? connectionString = TestConnectionString)
    {
        var values = new Dictionary<string, string?>
        {
            ["TripSafety:EnableSqlPersistence"] =
                enabled.ToString()
        };

        if (connectionString is not null)
        {
            values["ConnectionStrings:WayhunterSharedDb"] =
                connectionString;
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    private static void AddExistingInMemoryRegistrations(
        IServiceCollection services)
    {
        services.AddSingleton<
            InMemoryTripSharingConsentRepository>();

        services.AddSingleton<ITripSharingConsentRepository>(
            sp => sp.GetRequiredService<
                InMemoryTripSharingConsentRepository>());

        services.AddSingleton<ITripLiveLocationAccessReader>(
            sp => sp.GetRequiredService<
                InMemoryTripSharingConsentRepository>());
    }

    [Fact]
    public void SqlDisabled_PreservesInMemoryConsent()
    {
        var services = new ServiceCollection();

        AddExistingInMemoryRegistrations(services);

        services.AddConfiguredTripSafetyPersistence(
            CreateConfiguration(false));

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var consent = scope.ServiceProvider
            .GetRequiredService<ITripSharingConsentRepository>();

        var access = scope.ServiceProvider
            .GetRequiredService<ITripLiveLocationAccessReader>();

        Assert.IsType<
            InMemoryTripSharingConsentRepository>(consent);

        Assert.Same(consent, access);
    }

    [Fact]
    public void SqlDisabled_DoesNotRequireSqlConnection()
    {
        var services = new ServiceCollection();

        AddExistingInMemoryRegistrations(services);

        services.AddConfiguredTripSafetyPersistence(
            CreateConfiguration(false, null));

        Assert.DoesNotContain(
            services,
            x => x.ServiceType ==
                typeof(WayhunterSharedDbContext));
    }

    [Fact]
    public void SqlEnabled_RegistersSqlRepositories()
    {
        var services = new ServiceCollection();

        AddExistingInMemoryRegistrations(services);

        services.AddConfiguredTripSafetyPersistence(
            CreateConfiguration(true));

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var live = scope.ServiceProvider
            .GetRequiredService<ILiveTripSessionRepository>();

        var invitation = scope.ServiceProvider
            .GetRequiredService<ITripInvitationDecisionRepository>();

        var revocation = scope.ServiceProvider
            .GetRequiredService<ITripMembershipRevocationRepository>();

        var consent = scope.ServiceProvider
            .GetRequiredService<ITripSharingConsentRepository>();

        Assert.IsType<EfLiveTripSessionRepository>(live);

        Assert.IsType<EfTripInvitationDecisionRepository>(
            invitation);

        Assert.IsType<EfTripMembershipRevocationRepository>(
            revocation);

        Assert.IsType<EfTripSharingConsentRepository>(
            consent);
    }

    [Fact]
    public void SqlEnabled_ConsentAndAccessUseSameScopedInstance()
    {
        var services = new ServiceCollection();

        AddExistingInMemoryRegistrations(services);

        services.AddConfiguredTripSafetyPersistence(
            CreateConfiguration(true));

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var consent = scope.ServiceProvider
            .GetRequiredService<ITripSharingConsentRepository>();

        var access = scope.ServiceProvider
            .GetRequiredService<ITripLiveLocationAccessReader>();

        Assert.Same(consent, access);
    }

    [Fact]
    public void SqlEnabled_UsesSharedSqlServerContext()
    {
        var services = new ServiceCollection();

        services.AddConfiguredTripSafetyPersistence(
            CreateConfiguration(true));

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<WayhunterSharedDbContext>();

        Assert.True(db.Database.IsSqlServer());
    }

    [Fact]
    public void SqlEnabled_ReplacesExistingInMemoryContracts()
    {
        var services = new ServiceCollection();

        AddExistingInMemoryRegistrations(services);

        services.AddConfiguredTripSafetyPersistence(
            CreateConfiguration(true));

        Assert.Single(
            services,
            x => x.ServiceType ==
                typeof(ITripSharingConsentRepository));

        Assert.Single(
            services,
            x => x.ServiceType ==
                typeof(ITripLiveLocationAccessReader));

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.IsType<EfTripSharingConsentRepository>(
            scope.ServiceProvider
                .GetRequiredService<ITripSharingConsentRepository>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SqlEnabled_MissingConnection_Throws(
        string? connectionString)
    {
        var services = new ServiceCollection();

        Assert.Throws<InvalidOperationException>(
            () => services.AddConfiguredTripSafetyPersistence(
                CreateConfiguration(true, connectionString)));
    }

    [Fact]
    public void SqlEnabled_DoesNotRegisterDuplicateSharedContext()
    {
        var services = new ServiceCollection();

        var configuration = CreateConfiguration(true);

        services.AddConfiguredTripSafetyPersistence(configuration);
        services.AddConfiguredTripSafetyPersistence(configuration);

        Assert.Single(
            services,
            x => x.ServiceType ==
                typeof(DbContextOptions<WayhunterSharedDbContext>));
    }
}
