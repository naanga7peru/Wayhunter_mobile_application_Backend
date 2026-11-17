
using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Wayhunter.Infrastructure.Persistence.Configurations.IdentitySocial;
using Wayhunter.Infrastructure.Persistence.Configurations.CoinsWallet;
using Wayhunter.Infrastructure.Persistence.Models.CoinsWallet;
using Xunit;

namespace Wayhunter.UnitTests.Modules.CoinsWallet;

public sealed class CoinWalletEfModelTests
{
    private sealed class ModelOnlyContext : DbContext
    {
        protected override void OnConfiguring(
            DbContextOptionsBuilder options)
        {
            options.UseSqlServer(
                "Server=localhost;" +
                "Database=Wayhunter_ModelOnly;" +
                "Trusted_Connection=True;" +
                "TrustServerCertificate=True");
        }

        protected override void OnModelCreating(
            ModelBuilder model)
        {
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
        }
    }

    [Fact]
    public void M6_FourSqlTablesAreMapped()
    {
        using var db = new ModelOnlyContext();

        Assert.Equal(
            "CoinWallets",
            db.Model.FindEntityType(
                typeof(CoinWalletRow))?.GetTableName());

        Assert.Equal(
            "CoinLedgerEntries",
            db.Model.FindEntityType(
                typeof(CoinLedgerEntryRow))?.GetTableName());

        Assert.Equal(
            "CoinReservations",
            db.Model.FindEntityType(
                typeof(CoinReservationRow))?.GetTableName());

        Assert.Equal(
            "CoinRuleVersions",
            db.Model.FindEntityType(
                typeof(CoinRuleVersionRow))?.GetTableName());
    }

    [Fact]
    public void Wallet_UserIdIsUniqueAndLinkedToM1()
    {
        using var db = new ModelOnlyContext();

        var entity = db.Model.FindEntityType(
            typeof(CoinWalletRow));

        Assert.NotNull(entity);

        Assert.Contains(entity.GetIndexes(), index =>
            index.IsUnique &&
            index.Properties.Single().Name ==
                nameof(CoinWalletRow.UserId));

        Assert.Contains(entity.GetForeignKeys(), fk =>
            fk.PrincipalEntityType.ClrType ==
                typeof(UserAccount));
    }

    [Fact]
    public void Wallet_RowVersionIsConcurrencyToken()
    {
        using var db = new ModelOnlyContext();

        var entity = db.Model.FindEntityType(
            typeof(CoinWalletRow));

        Assert.NotNull(entity);

        var property = entity.FindProperty(
            nameof(CoinWalletRow.RowVersion));

        Assert.NotNull(property);
        Assert.True(property.IsConcurrencyToken);
    }

    [Fact]
    public void Ledger_UsesUniqueRewardSource()
    {
        using var db = new ModelOnlyContext();

        var entity = db.Model.FindEntityType(
            typeof(CoinLedgerEntryRow));

        Assert.NotNull(entity);

        Assert.Contains(entity.GetIndexes(), index =>
            index.IsUnique &&
            index.Properties.Select(p => p.Name)
                .SequenceEqual(new[]
                {
                    nameof(CoinLedgerEntryRow.WalletId),
                    nameof(CoinLedgerEntryRow.RewardType),
                    nameof(CoinLedgerEntryRow.SourceRecordId)
                }));

        Assert.Contains(entity.GetForeignKeys(), fk =>
            fk.PrincipalEntityType.ClrType ==
                typeof(CoinWalletRow));
    }

    [Fact]
    public void Reservation_UsesUniqueRequestId()
    {
        using var db = new ModelOnlyContext();

        var entity = db.Model.FindEntityType(
            typeof(CoinReservationRow));

        Assert.NotNull(entity);

        Assert.Contains(entity.GetIndexes(), index =>
            index.IsUnique &&
            index.Properties.Single().Name ==
                nameof(CoinReservationRow.RequestId));

        Assert.Contains(entity.GetForeignKeys(), fk =>
            fk.PrincipalEntityType.ClrType ==
                typeof(CoinWalletRow));
    }

    [Fact]
    public void RuleVersion_HasUniqueVersionCode()
    {
        using var db = new ModelOnlyContext();

        var entity = db.Model.FindEntityType(
            typeof(CoinRuleVersionRow));

        Assert.NotNull(entity);

        Assert.Contains(entity.GetIndexes(), index =>
            index.IsUnique &&
            index.Properties.Single().Name ==
                nameof(CoinRuleVersionRow.VersionCode));

        var lkr = entity.FindProperty(
            nameof(CoinRuleVersionRow.VoucherReferenceLkr));

        Assert.NotNull(lkr);
        Assert.Equal("decimal(18,2)", lkr.GetColumnType());
    }
}
