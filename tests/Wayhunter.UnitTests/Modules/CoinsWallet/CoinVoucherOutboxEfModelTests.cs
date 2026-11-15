
using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Wayhunter.Infrastructure.Persistence.Configurations.CoinsWallet;
using Wayhunter.Infrastructure.Persistence.Configurations.IdentitySocial;
using Wayhunter.Infrastructure.Persistence.Models.CoinsWallet;
using Xunit;

namespace Wayhunter.UnitTests.Modules.CoinsWallet;

public sealed class CoinVoucherOutboxEfModelTests
{
    private sealed class ModelOnlyContext : DbContext
    {
        protected override void OnConfiguring(
            DbContextOptionsBuilder options)
        {
            // EF metadata only; no SQL connection is opened.
            options.UseSqlServer(
                "Server=localhost;" +
                "Database=Wayhunter_ModelOnly;" +
                "Trusted_Connection=True;" +
                "TrustServerCertificate=True");
        }

        protected override void OnModelCreating(
            ModelBuilder model)
        {
            // Existing M1 and M6 Phase 1 mappings.
            model.ApplyConfiguration(
                new UserAccountConfiguration());

            model.ApplyConfiguration(
                new CoinWalletRowConfiguration());

            model.ApplyConfiguration(
                new CoinLedgerEntryRowConfiguration());

            model.ApplyConfiguration(
                new CoinReservationRowConfiguration());

            model.ApplyConfiguration(
                new CoinRuleVersionRowConfiguration());

            // New M6 Phase 2 mappings.
            model.ApplyConfiguration(
                new SampleVoucherOfferRowConfiguration());

            model.ApplyConfiguration(
                new SampleVoucherRedemptionRequestRowConfiguration());

            model.ApplyConfiguration(
                new SampleVoucherInventoryRowConfiguration());

            model.ApplyConfiguration(
                new SampleVoucherIssuanceRowConfiguration());

            model.ApplyConfiguration(
                new CoinRewardOutboxRowConfiguration());

            model.ApplyConfiguration(
                new CoinRewardConsumerDeliveryRowConfiguration());
        }
    }

    [Fact]
    public void Phase2_SixTablesAreMapped()
    {
        using var db = new ModelOnlyContext();

        Assert.Equal(
            "SampleVoucherOffers",
            db.Model.FindEntityType(
                typeof(SampleVoucherOfferRow))?.GetTableName());

        Assert.Equal(
            "SampleVoucherRedemptionRequests",
            db.Model.FindEntityType(
                typeof(SampleVoucherRedemptionRequestRow))
                    ?.GetTableName());

        Assert.Equal(
            "SampleVoucherInventory",
            db.Model.FindEntityType(
                typeof(SampleVoucherInventoryRow))?.GetTableName());

        Assert.Equal(
            "SampleVoucherIssuances",
            db.Model.FindEntityType(
                typeof(SampleVoucherIssuanceRow))?.GetTableName());

        Assert.Equal(
            "CoinRewardOutbox",
            db.Model.FindEntityType(
                typeof(CoinRewardOutboxRow))?.GetTableName());

        Assert.Equal(
            "CoinRewardConsumerDeliveries",
            db.Model.FindEntityType(
                typeof(CoinRewardConsumerDeliveryRow))
                    ?.GetTableName());
    }

    [Fact]
    public void VoucherOffer_HasUniqueCodeAndDecimalLkr()
    {
        using var db = new ModelOnlyContext();

        var entity = db.Model.FindEntityType(
            typeof(SampleVoucherOfferRow));

        Assert.NotNull(entity);

        Assert.Contains(entity.GetIndexes(), x =>
            x.IsUnique &&
            x.Properties.Count == 1 &&
            x.Properties[0].Name ==
                nameof(SampleVoucherOfferRow.OfferCode));

        var lkr = entity.FindProperty(
            nameof(SampleVoucherOfferRow.ReferenceValueLkr));

        Assert.NotNull(lkr);
        Assert.Equal("decimal(18,2)", lkr.GetColumnType());
    }

    [Fact]
    public void RedemptionRequest_HasWalletAndOfferForeignKeys()
    {
        using var db = new ModelOnlyContext();

        var entity = db.Model.FindEntityType(
            typeof(SampleVoucherRedemptionRequestRow));

        Assert.NotNull(entity);

        Assert.Contains(entity.GetForeignKeys(), fk =>
            fk.PrincipalEntityType.ClrType ==
                typeof(CoinWalletRow) &&
            fk.Properties.Single().Name ==
                nameof(SampleVoucherRedemptionRequestRow.WalletId));

        Assert.Contains(entity.GetForeignKeys(), fk =>
            fk.PrincipalEntityType.ClrType ==
                typeof(SampleVoucherOfferRow) &&
            fk.Properties.Single().Name ==
                nameof(SampleVoucherRedemptionRequestRow.OfferId));
    }

    [Fact]
    public void Inventory_HasUniqueCodeAndAssignment()
    {
        using var db = new ModelOnlyContext();

        var entity = db.Model.FindEntityType(
            typeof(SampleVoucherInventoryRow));

        Assert.NotNull(entity);

        Assert.Contains(entity.GetIndexes(), index =>
            index.IsUnique &&
            index.Properties.Single().Name ==
                nameof(SampleVoucherInventoryRow.SampleCode));

        Assert.Contains(entity.GetIndexes(), index =>
            index.IsUnique &&
            index.Properties.Single().Name ==
                nameof(SampleVoucherInventoryRow.AssignedRequestId) &&
            index.GetFilter() ==
                "[AssignedRequestId] IS NOT NULL");
    }

    [Fact]
    public void Issuance_RequestAndInventoryAreUnique()
    {
        using var db = new ModelOnlyContext();

        var entity = db.Model.FindEntityType(
            typeof(SampleVoucherIssuanceRow));

        Assert.NotNull(entity);

        Assert.Contains(entity.GetIndexes(), index =>
            index.IsUnique &&
            index.Properties.Single().Name ==
                nameof(SampleVoucherIssuanceRow.RequestId));

        Assert.Contains(entity.GetIndexes(), index =>
            index.IsUnique &&
            index.Properties.Single().Name ==
                nameof(SampleVoucherIssuanceRow.InventoryId));

        Assert.Equal(2, entity.GetForeignKeys().Count());
    }

    [Fact]
    public void Outbox_UsesGlobalRewardSourceUniqueness()
    {
        using var db = new ModelOnlyContext();

        var entity = db.Model.FindEntityType(
            typeof(CoinRewardOutboxRow));

        Assert.NotNull(entity);

        Assert.Contains(entity.GetIndexes(), index =>
            index.IsUnique &&
            index.Properties.Select(x => x.Name)
                .SequenceEqual(new[]
                {
                    nameof(CoinRewardOutboxRow.RewardType),
                    nameof(CoinRewardOutboxRow.SourceRecordId)
                }));
    }

    [Fact]
    public void Outbox_HasDueIndexAndConcurrencyToken()
    {
        using var db = new ModelOnlyContext();

        var entity = db.Model.FindEntityType(
            typeof(CoinRewardOutboxRow));

        Assert.NotNull(entity);

        Assert.Contains(entity.GetIndexes(), index =>
            index.Properties.Select(x => x.Name)
                .SequenceEqual(new[]
                {
                    nameof(CoinRewardOutboxRow.Status),
                    nameof(CoinRewardOutboxRow.NextDueAtUtc)
                }));

        var version = entity.FindProperty(
            nameof(CoinRewardOutboxRow.RowVersion));

        Assert.NotNull(version);
        Assert.True(version.IsConcurrencyToken);
    }

    [Fact]
    public void ConsumerDelivery_IsUniquePerEventAndConsumer()
    {
        using var db = new ModelOnlyContext();

        var entity = db.Model.FindEntityType(
            typeof(CoinRewardConsumerDeliveryRow));

        Assert.NotNull(entity);

        Assert.Contains(entity.GetIndexes(), index =>
            index.IsUnique &&
            index.Properties.Select(x => x.Name)
                .SequenceEqual(new[]
                {
                    nameof(CoinRewardConsumerDeliveryRow.EventId),
                    nameof(CoinRewardConsumerDeliveryRow.ConsumerName)
                }));

        Assert.Contains(entity.GetForeignKeys(), fk =>
            fk.PrincipalEntityType.ClrType ==
                typeof(CoinRewardOutboxRow));
    }

    [Fact]
    public void Phase2_ModelHasNoDuplicateTableNames()
    {
        using var db = new ModelOnlyContext();

        var tables = db.Model.GetEntityTypes()
            .Select(x => x.GetTableName())
            .Where(x => x is not null)
            .ToArray();

        Assert.Equal(
            tables.Length,
            tables.Distinct(StringComparer.OrdinalIgnoreCase)
                .Count());
    }
}
