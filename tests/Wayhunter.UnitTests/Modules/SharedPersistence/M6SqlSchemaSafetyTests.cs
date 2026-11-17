
using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Wayhunter.Infrastructure.Persistence.Models.CoinsWallet;
using Xunit;

namespace Wayhunter.UnitTests.Modules.SharedPersistence;

public sealed class M6SqlSchemaSafetyTests
{
    private static WayhunterSharedDbContext CreateContext()
    {
        // SQL Server model only.
        // No database connection or migration.
        var options =
            new DbContextOptionsBuilder<WayhunterSharedDbContext>()
                .UseSqlServer(
                    "Server=localhost;" +
                    "Database=Wayhunter_ModelAudit;" +
                    "Trusted_Connection=True;" +
                    "TrustServerCertificate=True")
                .Options;

        return new WayhunterSharedDbContext(options);
    }

    [Fact]
    public void GeneratedSql_ContainsAllTenM6Tables()
    {
        using var db = CreateContext();

        string sql = db.Database.GenerateCreateScript();

        string[] tables =
        {
            "CoinWallets",
            "CoinLedgerEntries",
            "CoinReservations",
            "CoinRuleVersions",
            "SampleVoucherOffers",
            "SampleVoucherRedemptionRequests",
            "SampleVoucherInventory",
            "SampleVoucherIssuances",
            "CoinRewardOutbox",
            "CoinRewardConsumerDeliveries"
        };

        foreach (string table in tables)
        {
            Assert.Contains(
                $"CREATE TABLE [{table}]",
                sql);
        }
    }

    [Fact]
    public void SqlSchema_ContainsCriticalCheckConstraints()
    {
        using var db = CreateContext();

        string sql = db.Database.GenerateCreateScript();

        string[] constraints =
        {
            "CK_CoinWallets_Balances",
            "CK_CoinLedgerEntries_NonZero",
            "CK_CoinReservations_Amount",
            "CK_CoinReservations_Status",
            "CK_CoinRuleVersions_Values",
            "CK_VoucherOffers_Values",
            "CK_VoucherOffers_Window",
            "CK_VoucherRequests_Amount",
            "CK_VoucherRequests_Status",
            "CK_VoucherIssuances_Status",
            "CK_CoinRewardOutbox_Amount",
            "CK_CoinRewardOutbox_Attempts",
            "CK_CoinRewardOutbox_Status",
            "CK_CoinRewardConsumerDeliveries_Attempts",
            "CK_CoinRewardConsumerDeliveries_Status"
        };

        foreach (string constraint in constraints)
        {
            Assert.Contains(constraint, sql);
        }
    }

    [Fact]
    public void RewardSources_AreRecipientScoped()
    {
        using var db = CreateContext();

        var ledger = db.Model.FindEntityType(
            typeof(CoinLedgerEntryRow));

        var outbox = db.Model.FindEntityType(
            typeof(CoinRewardOutboxRow));

        Assert.NotNull(ledger);
        Assert.NotNull(outbox);

        string[] ledgerExpected =
        {
            nameof(CoinLedgerEntryRow.WalletId),
            nameof(CoinLedgerEntryRow.RewardType),
            nameof(CoinLedgerEntryRow.SourceRecordId)
        };

        string[] outboxExpected =
        {
            nameof(CoinRewardOutboxRow.RewardType),
            nameof(CoinRewardOutboxRow.SourceRecordId),
            nameof(CoinRewardOutboxRow.RecipientUserId)
        };

        Assert.Contains(ledger.GetIndexes(), index =>
            index.IsUnique &&
            index.Properties.Select(x => x.Name)
                .SequenceEqual(ledgerExpected));

        Assert.Contains(outbox.GetIndexes(), index =>
            index.IsUnique &&
            index.Properties.Select(x => x.Name)
                .SequenceEqual(outboxExpected));
    }

    [Fact]
    public void VoucherIssuance_HasUniqueRequestAndInventory()
    {
        using var db = CreateContext();

        var entity = db.Model.FindEntityType(
            typeof(SampleVoucherIssuanceRow));

        Assert.NotNull(entity);

        Assert.Contains(entity.GetIndexes(), index =>
            index.IsUnique &&
            index.Properties.Count == 1 &&
            index.Properties[0].Name ==
                nameof(SampleVoucherIssuanceRow.RequestId));

        Assert.Contains(entity.GetIndexes(), index =>
            index.IsUnique &&
            index.Properties.Count == 1 &&
            index.Properties[0].Name ==
                nameof(SampleVoucherIssuanceRow.InventoryId));
    }

    [Fact]
    public void WalletAndOutbox_HaveConcurrencyTokens()
    {
        using var db = CreateContext();

        var wallet = db.Model.FindEntityType(
            typeof(CoinWalletRow));

        var outbox = db.Model.FindEntityType(
            typeof(CoinRewardOutboxRow));

        Assert.NotNull(wallet);
        Assert.NotNull(outbox);

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
    public void M6Relationships_DoNotCascadeDelete()
    {
        using var db = CreateContext();

        Type[] types =
        {
            typeof(CoinWalletRow),
            typeof(CoinLedgerEntryRow),
            typeof(CoinReservationRow),
            typeof(SampleVoucherRedemptionRequestRow),
            typeof(SampleVoucherInventoryRow),
            typeof(SampleVoucherIssuanceRow),
            typeof(CoinRewardConsumerDeliveryRow)
        };

        foreach (Type type in types)
        {
            var entity = db.Model.FindEntityType(type);

            Assert.NotNull(entity);

            foreach (var foreignKey in entity.GetForeignKeys())
            {
                Assert.Equal(
                    DeleteBehavior.NoAction,
                    foreignKey.DeleteBehavior);
            }
        }
    }
}
