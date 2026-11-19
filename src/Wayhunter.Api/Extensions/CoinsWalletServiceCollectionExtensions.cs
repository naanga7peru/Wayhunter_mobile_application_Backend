
using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wayhunter.Application.Modules.CoinsWallet.Interfaces;
using Wayhunter.Application.Modules.CoinsWallet.Queries;
using Wayhunter.Infrastructure.Persistence.Repositories.CoinsWallet;

namespace Wayhunter.Api.Extensions;

public static class CoinsWalletServiceCollectionExtensions
{
    public static IServiceCollection AddCoinsWalletSqlReader(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddWayhunterSharedPersistence(configuration);

        services.TryAddScoped<
            ICoinWalletReader,
            SqlCoinWalletReader>();

        services.TryAddScoped<GetMyCoinWalletService>();

        return services;
    }

    public static IServiceCollection AddCoinsWalletSqlRewardWriter(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddWayhunterSharedPersistence(configuration);

        services.TryAddScoped<
            ICoinRewardGrantStore,
            SqlCoinRewardGrantStore>();

        // M6 SQL Reward Outbox - explicit opt-in only.
        if (configuration.GetValue<bool>(
            "CoinsWallet:EnableSqlRewardOutboxStore"))
        {
            services.TryAddScoped<
                ICoinRewardOutboxStore,
                SqlCoinRewardOutboxStore>();
        }

        return services;
    }
}

