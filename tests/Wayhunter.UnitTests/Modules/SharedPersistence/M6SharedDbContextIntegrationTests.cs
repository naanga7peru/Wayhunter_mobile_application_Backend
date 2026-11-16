
using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Wayhunter.Domain.Modules.TravelPlanning.Entities;
using Wayhunter.Domain.Modules.TripSafety.Entities;
using Wayhunter.Domain.Modules.HomeAdsAnalytics.Entities;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Wayhunter.Infrastructure.Persistence.Models.CoinsWallet;
using Xunit;

namespace Wayhunter.UnitTests.Modules.SharedPersistence;

public sealed class M6SharedDbContextIntegrationTests
{
    private static WayhunterSharedDbContext CreateContext()
    {
        // Metadata test only. No SQL connection is opened.
        var options =
            new DbContextOptionsBuilder<WayhunterSharedDbContext>()
                .UseSqlServer(
                    "Server=localhost;" +
                    "Database=Wayhunter_ModelOnly;" +
                    "Trusted_Connection=True;" +
                    "TrustServerCertificate=True")
                .Options;

        return new WayhunterSharedDbContext(options);
    }

    private static IEntityType Entity(
        WayhunterSharedDbContext context,
        Type entityType)
    {
        return context.Model.FindEntityType(entityType)
            ?? throw new InvalidOperationException(
                $"Entity not mapped: {entityType.Name}");
    }

    private static bool HasForeignKey(
        IEntityType entity,
        Type principalType,
        string propertyName)
    {
        return entity.GetForeignKeys().Any(fk =>
            fk.PrincipalEntityType.ClrType == principalType &&
            fk.Properties.Any(p => p.Name == propertyName));
    }

    private static bool HasUniqueIndex(
        IEntityType entity,
        params string[] propertyNames)
    {
        return entity.GetIndexes().Any(index =>
            index.IsUnique &&
            index.Properties.Select(p => p.Name)
                .SequenceEqual(propertyNames));
    }

    [Theory]
    [InlineData(typeof(CoinWalletRow), "CoinWallets")]
    [InlineData(typeof(CoinLedgerEntryRow), "CoinLedgerEntries")]
    [InlineData(typeof(CoinReservationRow), "CoinReservations")]
    [InlineData(typeof(CoinRuleVersionRow), "CoinRuleVersions")]
    [InlineData(typeof(SampleVoucherOfferRow), "SampleVoucherOffers")]
    [InlineData(
        typeof(SampleVoucherRedemptionRequestRow),
        "SampleVoucherRedemptionRequests")]
    [InlineData(
        typeof(SampleVoucherInventoryRow),
        "SampleVoucherInventory")]
    [InlineData(
        typeof(SampleVoucherIssuanceRow),
        "SampleVoucherIssuances")]
    [InlineData(typeof(CoinRewardOutboxRow), "CoinRewardOutbox")]
    [InlineData(
        typeof(CoinRewardConsumerDeliveryRow),
        "CoinRewardConsumerDeliveries")]
    public void AllTenM6Tables_AreMapped(
        Type entityType,
        string expectedTable)
    {
        using var context = CreateContext();

        Assert.Equal(
            expectedTable,
            Entity(context, entityType).GetTableName());
    }

    [Fact]
    public void ExistingModuleMappings_ArePreserved()
    {
        using var context = CreateContext();

        Assert.NotNull(context.Model.FindEntityType(
            typeof(UserAccount)));

        Assert.NotNull(context.Model.FindEntityType(
            typeof(TripPlan)));

        Assert.NotNull(context.Model.FindEntityType(
            typeof(TripStop)));

        Assert.NotNull(context.Model.FindEntityType(
            typeof(LiveTripSession)));

        Assert.NotNull(context.Model.FindEntityType(
            typeof(Advertisement)));
    }

    [Fact]
    public void Wallet_HasM1UserForeignKey()
    {
        using var context = CreateContext();

        var wallet = Entity(context, typeof(CoinWalletRow));

        Assert.True(HasForeignKey(
            wallet,
            typeof(UserAccount),
            nameof(CoinWalletRow.UserId)));

        Assert.True(HasUniqueIndex(
            wallet,
            nameof(CoinWalletRow.UserId)));
    }

    [Fact]
    public void LedgerAndReservation_HaveWalletForeignKeys()
    {
        using var context = CreateContext();

        var ledger = Entity(
            context, typeof(CoinLedgerEntryRow));

        var reservation = Entity(
            context, typeof(CoinReservationRow));

        Assert.True(HasForeignKey(
            ledger,
            typeof(CoinWalletRow),
            nameof(CoinLedgerEntryRow.WalletId)));

        Assert.True(HasForeignKey(
            reservation,
            typeof(CoinWalletRow),
            nameof(CoinReservationRow.WalletId)));

        Assert.True(HasUniqueIndex(
            reservation,
            nameof(CoinReservationRow.RequestId)));
    }

    [Fact]
    public void VoucherRelationships_AreConnected()
    {
        using var context = CreateContext();

        var request = Entity(
            context,
            typeof(SampleVoucherRedemptionRequestRow));

        Assert.True(HasForeignKey(
            request,
            typeof(CoinWalletRow),
            nameof(SampleVoucherRedemptionRequestRow.WalletId)));

        Assert.True(HasForeignKey(
            request,
            typeof(SampleVoucherOfferRow),
            nameof(SampleVoucherRedemptionRequestRow.OfferId)));

        var inventory = Entity(
            context,
            typeof(SampleVoucherInventoryRow));

        Assert.True(HasForeignKey(
            inventory,
            typeof(SampleVoucherOfferRow),
            nameof(SampleVoucherInventoryRow.OfferId)));

        var issuance = Entity(
            context,
            typeof(SampleVoucherIssuanceRow));

        Assert.True(HasForeignKey(
            issuance,
            typeof(SampleVoucherRedemptionRequestRow),
            nameof(SampleVoucherIssuanceRow.RequestId)));

        Assert.True(HasForeignKey(
            issuance,
            typeof(SampleVoucherInventoryRow),
            nameof(SampleVoucherIssuanceRow.InventoryId)));
    }

    [Fact]
    public void RewardSourceKeys_AreUniqueAcrossRecipients()
    {
        using var context = CreateContext();

        var ledger = Entity(
            context, typeof(CoinLedgerEntryRow));

        var outbox = Entity(
            context, typeof(CoinRewardOutboxRow));

        Assert.True(HasUniqueIndex(
            ledger,
            nameof(CoinLedgerEntryRow.RewardType),
            nameof(CoinLedgerEntryRow.SourceRecordId)));

        Assert.True(HasUniqueIndex(
            outbox,
            nameof(CoinRewardOutboxRow.RewardType),
            nameof(CoinRewardOutboxRow.SourceRecordId)));
    }

    [Fact]
    public void ConsumerDelivery_HasOutboxForeignKeyAndUniqueIndex()
    {
        using var context = CreateContext();

        var delivery = Entity(
            context,
            typeof(CoinRewardConsumerDeliveryRow));

        Assert.True(HasForeignKey(
            delivery,
            typeof(CoinRewardOutboxRow),
            nameof(CoinRewardConsumerDeliveryRow.EventId)));

        Assert.True(HasUniqueIndex(
            delivery,
            nameof(CoinRewardConsumerDeliveryRow.EventId),
            nameof(CoinRewardConsumerDeliveryRow.ConsumerName)));
    }

    [Fact]
    public void WalletAndOutbox_HaveConcurrencyTokens()
    {
        using var context = CreateContext();

        var wallet = Entity(
            context, typeof(CoinWalletRow));

        var outbox = Entity(
            context, typeof(CoinRewardOutboxRow));

        var walletVersion = wallet.FindProperty(
            nameof(CoinWalletRow.RowVersion));

        var outboxVersion = outbox.FindProperty(
            nameof(CoinRewardOutboxRow.RowVersion));

        Assert.NotNull(walletVersion);
        Assert.NotNull(outboxVersion);

        Assert.True(walletVersion.IsConcurrencyToken);
        Assert.True(outboxVersion.IsConcurrencyToken);
    }

    [Fact]
    public void SharedModel_HasNoDuplicateTableNames()
    {
        using var context = CreateContext();

        var tableNames = context.Model.GetEntityTypes()
            .Select(x => x.GetTableName())
            .Where(x => x is not null)
            .ToArray();

        Assert.Equal(
            tableNames.Length,
            tableNames.Distinct(
                StringComparer.OrdinalIgnoreCase).Count());
    }
}
