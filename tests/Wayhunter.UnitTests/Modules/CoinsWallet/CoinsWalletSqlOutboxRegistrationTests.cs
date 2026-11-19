using System;
using System.Collections.Generic;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Wayhunter.Api.Extensions;
using Wayhunter.Application.Modules.CoinsWallet.Interfaces;

using Wayhunter.Infrastructure.Persistence.DbContexts;
using Wayhunter.Infrastructure.Persistence.Repositories.CoinsWallet;

using Xunit;

namespace Wayhunter.UnitTests.Modules.CoinsWallet;

public sealed class CoinsWalletSqlOutboxRegistrationTests
{
    private static IConfiguration Config(
        bool enableOutbox = false,
        bool includeConnectionString = true)
    {
        var values = new Dictionary<string, string?>
        {
            ["CoinsWallet:EnableSqlRewardOutboxStore"] =
                enableOutbox ? "true" : "false"
        };

        if (includeConnectionString)
        {
            values["ConnectionStrings:WayhunterSharedDb"] =
                "Server=localhost;" +
                "Database=WayhunterM6OutboxRegistrationTest;" +
                "Trusted_Connection=True;" +
                "TrustServerCertificate=True";
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    [Fact]
    public void Default_DoesNotRegisterOutbox()
    {
        var services = new ServiceCollection();

        services.AddCoinsWalletSqlRewardWriter(Config());

        Assert.DoesNotContain(
            services,
            x => x.ServiceType == typeof(ICoinRewardOutboxStore));
    }

    [Fact]
    public void OptIn_RegistersSqlOutbox()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddCoinsWalletSqlRewardWriter(
            Config(enableOutbox: true));

        using var provider =
            services.BuildServiceProvider(validateScopes: true);

        using var scope = provider.CreateScope();

        Assert.IsType<SqlCoinRewardOutboxStore>(
            scope.ServiceProvider
                .GetRequiredService<ICoinRewardOutboxStore>());
    }

    [Fact]
    public void RepeatedRegistration_IsIdempotent()
    {
        var services = new ServiceCollection();
        var config = Config(enableOutbox: true);

        services.AddCoinsWalletSqlRewardWriter(config);
        services.AddCoinsWalletSqlRewardWriter(config);

        Assert.Single(
            services,
            x => x.ServiceType == typeof(ICoinRewardOutboxStore));

        Assert.Single(
            services,
            x => x.ServiceType == typeof(ICoinRewardGrantStore));
    }

    [Fact]
    public void ReaderWriterAndOutbox_ShareContext()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        var config = Config(enableOutbox: true);

        services.AddCoinsWalletSqlReader(config);
        services.AddCoinsWalletSqlRewardWriter(config);

        using var provider =
            services.BuildServiceProvider(validateScopes: true);

        using var scope = provider.CreateScope();

        Assert.IsType<SqlCoinWalletReader>(
            scope.ServiceProvider
                .GetRequiredService<ICoinWalletReader>());

        Assert.IsType<SqlCoinRewardGrantStore>(
            scope.ServiceProvider
                .GetRequiredService<ICoinRewardGrantStore>());

        Assert.IsType<SqlCoinRewardOutboxStore>(
            scope.ServiceProvider
                .GetRequiredService<ICoinRewardOutboxStore>());

        var db = scope.ServiceProvider
            .GetRequiredService<WayhunterSharedDbContext>();

        Assert.True(db.Database.IsSqlServer());
    }

    [Fact]
    public void ReaderAlone_DoesNotRegisterOutbox()
    {
        var services = new ServiceCollection();

        services.AddCoinsWalletSqlReader(
            Config(enableOutbox: true));

        Assert.DoesNotContain(
            services,
            x => x.ServiceType == typeof(ICoinRewardOutboxStore));
    }

    [Fact]
    public void MissingConnectionString_IsRejected()
    {
        var services = new ServiceCollection();

        Assert.Throws<InvalidOperationException>(() =>
            services.AddCoinsWalletSqlRewardWriter(
                Config(
                    enableOutbox: true,
                    includeConnectionString: false)));
    }
}
