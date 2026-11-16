
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Wayhunter.Infrastructure.Persistence.Models.CoinsWallet;

namespace Wayhunter.Infrastructure.Persistence.Configurations.CoinsWallet;

public sealed class CoinWalletRowConfiguration
    : IEntityTypeConfiguration<CoinWalletRow>
{
    public void Configure(EntityTypeBuilder<CoinWalletRow> b)
    {
        b.ToTable("CoinWallets", t =>
        {
            t.HasCheckConstraint(
                "CK_CoinWallets_Balances",
                "[PostedCoins] >= 0 AND " +
                "[ReservedCoins] >= 0 AND " +
                "[LifetimeEarnedCoins] >= 0 AND " +
                "[ReservedCoins] <= [PostedCoins]");
        });

        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();

        b.Property(x => x.UserId)
            .HasMaxLength(256)
            .IsRequired();

        b.Property(x => x.PostedCoins)
            .HasColumnType("bigint");

        b.Property(x => x.ReservedCoins)
            .HasColumnType("bigint");

        b.Property(x => x.LifetimeEarnedCoins)
            .HasColumnType("bigint");

        b.Property(x => x.RowVersion).IsRowVersion();

        b.HasIndex(x => x.UserId)
            .IsUnique()
            .HasDatabaseName("UX_CoinWallets_UserId");

        b.HasOne<UserAccount>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}

public sealed class CoinLedgerEntryRowConfiguration
    : IEntityTypeConfiguration<CoinLedgerEntryRow>
{
    public void Configure(
        EntityTypeBuilder<CoinLedgerEntryRow> b)
    {
        b.ToTable("CoinLedgerEntries", t =>
        {
            t.HasCheckConstraint(
                "CK_CoinLedgerEntries_NonZero",
                "[DeltaCoins] <> 0");
        });

        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();

        b.Property(x => x.DeltaCoins)
            .HasColumnType("bigint");

        b.Property(x => x.RewardType)
            .HasConversion<int>();

        b.Property(x => x.SourceRecordId)
            .HasMaxLength(256)
            .IsRequired();

        b.Property(x => x.RuleVersion)
            .HasMaxLength(64)
            .IsRequired();

        b.Property(x => x.RecordedAtUtc)
            .HasColumnType("datetimeoffset");

        b.HasOne<CoinWalletRow>()
            .WithMany()
            .HasForeignKey(x => x.WalletId)
            .OnDelete(DeleteBehavior.NoAction);

        // Prevent duplicate reward source credits
        // even if the recipient UserId changes.
        b.HasIndex(x => new
        {
            x.RewardType,
            x.SourceRecordId
        })
        .IsUnique()
        .HasFilter("[RewardType] IS NOT NULL")
        .HasDatabaseName(
            "UX_CoinLedgerEntries_RewardSource");

        b.HasIndex(x => x.WalletId);
    }
}

public sealed class CoinReservationRowConfiguration
    : IEntityTypeConfiguration<CoinReservationRow>
{
    public void Configure(
        EntityTypeBuilder<CoinReservationRow> b)
    {
        b.ToTable("CoinReservations", t =>
        {
            t.HasCheckConstraint(
                "CK_CoinReservations_Amount",
                "[ReservedCoins] > 0");

            t.HasCheckConstraint(
                "CK_CoinReservations_Status",
                "[Status] IN (1, 2, 3)");
        });

        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();

        b.Property(x => x.ReservedCoins)
            .HasColumnType("bigint");

        b.Property(x => x.Status)
            .HasConversion<int>();

        b.Property(x => x.CreatedAtUtc)
            .HasColumnType("datetimeoffset");

        b.Property(x => x.ResolvedAtUtc)
            .HasColumnType("datetimeoffset");

        b.HasOne<CoinWalletRow>()
            .WithMany()
            .HasForeignKey(x => x.WalletId)
            .OnDelete(DeleteBehavior.NoAction);

        b.HasIndex(x => x.RequestId)
            .IsUnique()
            .HasDatabaseName(
                "UX_CoinReservations_RequestId");
    }
}

public sealed class CoinRuleVersionRowConfiguration
    : IEntityTypeConfiguration<CoinRuleVersionRow>
{
    public void Configure(
        EntityTypeBuilder<CoinRuleVersionRow> b)
    {
        b.ToTable("CoinRuleVersions", t =>
        {
            t.HasCheckConstraint(
                "CK_CoinRuleVersions_Values",
                "[VoucherCostCoins] > 0 AND " +
                "[VoucherReferenceLkr] >= 0");
        });

        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();

        b.Property(x => x.VersionCode)
            .HasMaxLength(64)
            .IsRequired();

        b.Property(x => x.VoucherCostCoins)
            .HasColumnType("bigint");

        b.Property(x => x.VoucherReferenceLkr)
            .HasColumnType("decimal(18,2)");

        b.HasIndex(x => x.VersionCode)
            .IsUnique()
            .HasDatabaseName(
                "UX_CoinRuleVersions_VersionCode");
    }
}
