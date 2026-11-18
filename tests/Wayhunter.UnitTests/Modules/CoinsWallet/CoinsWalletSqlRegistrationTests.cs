
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
                typeof(DbContextOptions<WayhunterSharedDbContext>));
    }
}
