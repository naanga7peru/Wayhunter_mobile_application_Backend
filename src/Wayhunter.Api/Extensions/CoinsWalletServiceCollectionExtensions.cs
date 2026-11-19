
using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wayhunter.Application.Modules.CoinsWallet.Interfaces;
using Wayhunter.Application.Modules.CoinsWallet.Commands;
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

        services.TryAddScoped<
            ICoinTransactionHistoryReader,
            SqlCoinTransactionHistoryReader>();

        services.TryAddScoped<
            GetMyCoinTransactionHistoryService>();

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

        services.TryAddScoped<
            IVerifiedWelcomeAccountReader,
            SqlVerifiedWelcomeAccountReader>();

        services.TryAddScoped<
            IHunterApprovedRewardReader,
            SqlHunterApprovedRewardReader>();

        services.TryAddScoped<SqlGameApprovedRewardReader>();
        services.TryAddScoped<IGameCreatorApprovedRewardReader>(sp =>
            sp.GetRequiredService<SqlGameApprovedRewardReader>());
        services.TryAddScoped<IGameWinnerApprovedRewardReader>(sp =>
            sp.GetRequiredService<SqlGameApprovedRewardReader>());

        services.TryAddScoped(sp =>
            new GrantVerifiedWelcomeCoinsHandler(
                sp.GetRequiredService<
                    IVerifiedWelcomeAccountReader>(),
                sp.GetRequiredService<
                    ICoinRewardGrantStore>(),
                configuration[
                    "CoinsWallet:RewardRuleVersion"]
                ?? "2026-11-11"));

        services.TryAddScoped<
            HandleVerifiedAccountActivatedReward>();

        string ruleVersion = configuration[
            "CoinsWallet:RewardRuleVersion"] ?? "2026-11-11";

        services.TryAddScoped(sp =>
            new GrantApprovedHunterCoinsHandler(
                sp.GetRequiredService<IHunterApprovedRewardReader>(),
                sp.GetRequiredService<ICoinRewardGrantStore>(),
                ruleVersion));
        services.TryAddScoped(sp =>
            new GrantApprovedGameCreatorCoinsHandler(
                sp.GetRequiredService<IGameCreatorApprovedRewardReader>(),
                sp.GetRequiredService<ICoinRewardGrantStore>(),
                ruleVersion));
        services.TryAddScoped(sp =>
            new GrantApprovedGameWinnerCoinsHandler(
                sp.GetRequiredService<IGameWinnerApprovedRewardReader>(),
                sp.GetRequiredService<ICoinRewardGrantStore>(),
                ruleVersion));

        // M6 SQL Reward Outbox - explicit opt-in only.
        if (configuration.GetValue<bool>(
            "CoinsWallet:EnableSqlRewardOutboxStore"))
        {
            services.TryAddScoped<
                ICoinRewardOutboxStore,
                SqlCoinRewardOutboxStore>();

            services.TryAddScoped<ICommittedCoinRewardSource>(sp =>
                new SqlCommittedCoinRewardSource(
                    sp.GetRequiredService<
                        Wayhunter.Infrastructure.Persistence.DbContexts.WayhunterSharedDbContext>(),
                    ruleVersion));
            services.TryAddScoped<ProcessPendingCoinRewardsService>();
            services.TryAddScoped<
                ICoinRewardConsumerDeliveryStore,
                SqlCoinRewardConsumerDeliveryStore>();
        }

        return services;
    }

    public static IServiceCollection AddCoinsWalletSqlVoucherRuntime(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddWayhunterSharedPersistence(configuration);

        services.TryAddScoped<
            ISampleVoucherRequestStore,
            SqlSampleVoucherRequestStore>();

        services.TryAddScoped<
            ISampleVoucherAdminStore,
            SqlSampleVoucherAdminStore>();

        services.TryAddScoped<
            ICoinAdministrationStore,
            SqlCoinAdministrationStore>();

        services.TryAddSingleton<IVoucherSecretProtector>(_ =>
            new AesVoucherSecretProtector(
                configuration[
                    "CoinsWallet:VoucherProtectionKey"]
                ?? string.Empty));

        services.TryAddScoped<SampleVoucherRequestService>();

        services.TryAddScoped(sp =>
            new SampleVoucherAdminService(
                sp.GetRequiredService<ISampleVoucherAdminStore>(),
                sp.GetRequiredService<TimeProvider>(),
                configuration[
                    "CoinsWallet:RewardRuleVersion"]
                ?? "2026-11-11",
                sp.GetRequiredService<ICoinAdministrationStore>()));

        return services;
    }
}

