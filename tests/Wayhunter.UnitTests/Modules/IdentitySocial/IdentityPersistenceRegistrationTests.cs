
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Wayhunter.Api.Extensions;
using Wayhunter.Api.Services.IdentitySocial;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Xunit;

namespace Wayhunter.UnitTests.Modules.IdentitySocial;

public sealed class IdentityPersistenceRegistrationTests
{
    private static IConfiguration CreateConfiguration(
        string? connectionString)
    {
        var values = new Dictionary<string, string?>();

        if (connectionString is not null)
        {
            values["ConnectionStrings:WayhunterSharedDb"] =
                connectionString;
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    [Fact]
    public void MissingConnectionString_IsRejected()
    {
        var services = new ServiceCollection();

        Assert.Throws<InvalidOperationException>(() =>
            services.AddWayhunterIdentityPersistence(
                CreateConfiguration(null)));
    }

    [Fact]
    public void BlankConnectionString_IsRejected()
    {
        var services = new ServiceCollection();

        Assert.Throws<InvalidOperationException>(() =>
            services.AddWayhunterIdentityPersistence(
                CreateConfiguration("   ")));
    }

    [Fact]
    public void ValidConfiguration_RegistersRequiredServices()
    {
        var services = new ServiceCollection();

        services.AddLogging();

        // Test-only SQL configuration.
        // No connection is opened.
        services.AddWayhunterIdentityPersistence(
            CreateConfiguration(
                "Server=localhost;" +
                "Database=WayhunterIdentityTest;" +
                "Trusted_Connection=True;" +
                "TrustServerCertificate=True"));

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.NotNull(
            scope.ServiceProvider
                .GetRequiredService<WayhunterSharedDbContext>());

        Assert.IsType<SqlAuthenticatedSessionReader>(
            scope.ServiceProvider
                .GetRequiredService<
                    IAuthenticatedSessionReader>());

        Assert.IsType<SqlTrustedIdentityAccountReader>(
            scope.ServiceProvider
                .GetRequiredService<
                    ITrustedIdentityAccountReader>());

        Assert.NotNull(
            scope.ServiceProvider
                .GetRequiredService<
                    TrustedIdentityResolutionService>());

        Assert.IsType<SqlIdentitySessionRevocationStore>(
            scope.ServiceProvider
                .GetRequiredService<
                    IIdentitySessionRevocationStore>());

        Assert.NotNull(
            scope.ServiceProvider
                .GetRequiredService<SessionLogoutService>());
    }

    [Fact]
    public void SharedDbContext_UsesSqlServerProvider()
    {
        var services = new ServiceCollection();

        services.AddLogging();

        services.AddWayhunterIdentityPersistence(
            CreateConfiguration(
                "Server=localhost;" +
                "Database=WayhunterIdentityTest;" +
                "Trusted_Connection=True;" +
                "TrustServerCertificate=True"));

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<WayhunterSharedDbContext>();

        Assert.True(db.Database.IsSqlServer());
    }

    [Fact]
    public void TrustedIdentityReader_IsScopedWithSharedDbContext()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddWayhunterIdentityPersistence(
            CreateConfiguration(
                "Server=localhost;" +
                "Database=WayhunterIdentityTest;" +
                "Trusted_Connection=True;" +
                "TrustServerCertificate=True"));

        using var provider = services.BuildServiceProvider();
        using var firstScope = provider.CreateScope();
        using var secondScope = provider.CreateScope();

        var firstReader = firstScope.ServiceProvider
            .GetRequiredService<ITrustedIdentityAccountReader>();
        Assert.IsType<SqlTrustedIdentityAccountReader>(firstReader);
        Assert.Equal(
            ServiceLifetime.Scoped,
            services.Single(descriptor => descriptor.ServiceType ==
                typeof(ITrustedIdentityAccountReader)).Lifetime);
        Assert.Same(
            firstReader,
            firstScope.ServiceProvider
                .GetRequiredService<ITrustedIdentityAccountReader>());
        Assert.NotSame(
            firstReader,
            secondScope.ServiceProvider
                .GetRequiredService<ITrustedIdentityAccountReader>());
    }
}
