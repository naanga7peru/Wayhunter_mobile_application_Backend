
using System;
using System.Collections.Generic;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Wayhunter.Api.Extensions;

using Wayhunter.Application.Modules.CoinsWallet.Interfaces;
using Wayhunter.Application.Modules.CoinsWallet.Queries;

using Wayhunter.Infrastructure.Persistence.DbContexts;
using Wayhunter.Infrastructure.Persistence.Repositories.CoinsWallet;

using Xunit;

namespace Wayhunter.UnitTests.Modules.CoinsWallet;

public sealed class CoinsWalletSqlRegistrationTests
{
    private static IConfiguration CreateConfiguration(
        bool includeConnectionString = true)
    {
        var values = new Dictionary<string, string?>();

        if (includeConnectionString)
        {
            values["ConnectionStrings:WayhunterSharedDb"] =
                "Server=localhost;" +
                "Database=WayhunterM6RegistrationTest;" +
                "Trusted_Connection=True;" +
                "TrustServerCertificate=True";
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    // ==========================================
    // SQL Reader Registration Tests
    // ==========================================

    [Fact]
    public void MissingConnectionString_IsRejected()
    {
        var services = new ServiceCollection();

        Assert.Throws<InvalidOperationException>(() =>
            services.AddCoinsWalletSqlReader(
                CreateConfiguration(false)));
    }

    [Fact]
    public void ValidConfiguration_RegistersSqlReaderAndService()
    {
        var services = new ServiceCollection();

        services.AddLogging();

        services.AddCoinsWalletSqlReader(
            CreateConfiguration());

        using var provider = services.BuildServiceProvider(
            validateScopes: true);

        using var scope = provider.CreateScope();

        Assert.IsType<SqlCoinWalletReader>(
            scope.ServiceProvider
                .GetRequiredService<ICoinWalletReader>());

        Assert.NotNull(
            scope.ServiceProvider
                .GetRequiredService<GetMyCoinWalletService>());

        var db = scope.ServiceProvider
            .GetRequiredService<WayhunterSharedDbContext>();

        Assert.True(db.Database.IsSqlServer());
    }

    [Fact]
    public void RepeatedRegistration_DoesNotDuplicateSharedContext()
    {
        var services = new ServiceCollection();

        var configuration = CreateConfiguration();

        services.AddCoinsWalletSqlReader(configuration);
        services.AddCoinsWalletSqlReader(configuration);

        using var provider = services.BuildServiceProvider();

        using var scope = provider.CreateScope();

        Assert.IsType<SqlCoinWalletReader>(
            scope.ServiceProvider
                .GetRequiredService<ICoinWalletReader>());

        Assert.Single(
            services,
            descriptor =>
                descriptor.ServiceType ==
                typeof(
                    DbContextOptions<WayhunterSharedDbContext>));
    }

    // ==========================================
    // SQL Reward Writer Registration Tests
    // ==========================================

    [Fact]
    public void RewardWriter_RegistersSqlStore()
    {
        var services = new ServiceCollection();

        services.AddLogging();

        services.AddCoinsWalletSqlRewardWriter(
            CreateConfiguration());

        using var provider = services.BuildServiceProvider(
            validateScopes: true);

        using var scope = provider.CreateScope();

        var store = scope.ServiceProvider
            .GetRequiredService<ICoinRewardGrantStore>();

        Assert.IsType<SqlCoinRewardGrantStore>(store);

        var db = scope.ServiceProvider
            .GetRequiredService<WayhunterSharedDbContext>();

        Assert.True(db.Database.IsSqlServer());
    }

    [Fact]
    public void RewardWriter_RepeatedRegistration_DoesNotDuplicateContext()
    {
        var services = new ServiceCollection();

        var configuration = CreateConfiguration();

        services.AddCoinsWalletSqlRewardWriter(configuration);
        services.AddCoinsWalletSqlRewardWriter(configuration);

        Assert.Single(
            services,
            descriptor =>
                descriptor.ServiceType ==
                typeof(ICoinRewardGrantStore));

        Assert.Single(
            services,
            descriptor =>
                descriptor.ServiceType ==
                typeof(
                    DbContextOptions<WayhunterSharedDbContext>));
    }

    // ==========================================
    // M6 - Reader + Writer Runtime Integration
    // ==========================================

    [Fact]
    public void ReaderAndRewardWriter_ShareSingleDbContext()
    {
        var services = new ServiceCollection();

        var configuration = CreateConfiguration();

        services.AddLogging();

        services.AddCoinsWalletSqlReader(configuration);
        services.AddCoinsWalletSqlRewardWriter(configuration);

        using var provider = services.BuildServiceProvider(
            validateScopes: true);

        using var scope = provider.CreateScope();

        var reader = scope.ServiceProvider
            .GetRequiredService<ICoinWalletReader>();

        var writer = scope.ServiceProvider
            .GetRequiredService<ICoinRewardGrantStore>();

        Assert.IsType<SqlCoinWalletReader>(reader);

        Assert.IsType<SqlCoinRewardGrantStore>(writer);

        Assert.NotNull(
            scope.ServiceProvider
                .GetRequiredService<GetMyCoinWalletService>());

        var db = scope.ServiceProvider
            .GetRequiredService<WayhunterSharedDbContext>();

        Assert.True(db.Database.IsSqlServer());

        Assert.Single(
            services,
            descriptor =>
                descriptor.ServiceType ==
                typeof(
                    DbContextOptions<WayhunterSharedDbContext>));

        Assert.Single(
            services,
            descriptor =>
                descriptor.ServiceType ==
                typeof(ICoinRewardGrantStore));
    }

    [Fact]
    public void ReaderAndRewardWriter_RepeatedRegistration_IsIdempotent()
    {
        var services = new ServiceCollection();

        var configuration = CreateConfiguration();

        services.AddCoinsWalletSqlReader(configuration);
        services.AddCoinsWalletSqlRewardWriter(configuration);

        services.AddCoinsWalletSqlReader(configuration);
        services.AddCoinsWalletSqlRewardWriter(configuration);

        Assert.Single(
            services,
            descriptor =>
                descriptor.ServiceType ==
                typeof(ICoinWalletReader));

        Assert.Single(
            services,
            descriptor =>
                descriptor.ServiceType ==
                typeof(ICoinRewardGrantStore));

        Assert.Single(
            services,
            descriptor =>
                descriptor.ServiceType ==
                typeof(
                    DbContextOptions<WayhunterSharedDbContext>));
    }
}
