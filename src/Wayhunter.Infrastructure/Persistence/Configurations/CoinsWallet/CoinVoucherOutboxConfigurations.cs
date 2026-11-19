
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wayhunter.Domain.Modules.CoinsWallet.Enums;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Wayhunter.Infrastructure.Persistence.Models.CoinsWallet;

namespace Wayhunter.Infrastructure.Persistence.Configurations.CoinsWallet;

public sealed class SampleVoucherOfferRowConfiguration
    : IEntityTypeConfiguration<SampleVoucherOfferRow>
{
    public void Configure(
        EntityTypeBuilder<SampleVoucherOfferRow> b)
    {
        b.ToTable("SampleVoucherOffers", t =>
        {
            t.HasCheckConstraint(
                "CK_VoucherOffers_Values",
                "[CostCoins] > 0 AND [ReferenceValueLkr] >= 0");

            t.HasCheckConstraint(
                "CK_VoucherOffers_Window",
                "[ValidUntilUtc] > [ValidFromUtc]");
        });

        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();

        b.Property(x => x.OfferCode)
            .HasMaxLength(100).IsRequired();

        b.Property(x => x.CostCoins)
            .HasColumnType("bigint");

        b.Property(x => x.ReferenceValueLkr)
            .HasColumnType("decimal(18,2)");

        b.Property(x => x.ValidFromUtc)
            .HasColumnType("datetimeoffset");

        b.Property(x => x.ValidUntilUtc)
            .HasColumnType("datetimeoffset");

        b.HasIndex(x => x.OfferCode)
            .IsUnique()
            .HasDatabaseName("UX_VoucherOffers_Code");
    }
}

public sealed class SampleVoucherRedemptionRequestRowConfiguration
    : IEntityTypeConfiguration<SampleVoucherRedemptionRequestRow>
{
    public void Configure(
        EntityTypeBuilder<SampleVoucherRedemptionRequestRow> b)
    {
        b.ToTable("SampleVoucherRedemptionRequests", t =>
        {
            t.HasCheckConstraint(
                "CK_VoucherRequests_Amount",
                "[CostCoins] > 0");

            t.HasCheckConstraint(
                "CK_VoucherRequests_Status",
                "[Status] IN (1, 2, 3, 4)");
        });

        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();

        b.Property(x => x.OwnerUserId)
            .HasMaxLength(256).IsRequired();

        b.Property(x => x.OwnerNameSnapshot)
            .HasMaxLength(200).IsRequired();

        b.Property(x => x.DecisionActorUserId)
            .HasMaxLength(256);

        b.Property(x => x.DecisionReason)
            .HasMaxLength(1000);

        b.Property(x => x.CostCoins)
            .HasColumnType("bigint");

        b.Property(x => x.Status)
            .HasConversion<int>().IsRequired();

        b.Property(x => x.RequestedAtUtc)
            .HasColumnType("datetimeoffset");

        b.Property(x => x.DecidedAtUtc)
            .HasColumnType("datetimeoffset");

        b.HasOne<CoinWalletRow>()
            .WithMany()
            .HasForeignKey(x => new { x.WalletId, x.OwnerUserId })
            .HasPrincipalKey(x => new { x.Id, x.UserId })
            .OnDelete(DeleteBehavior.NoAction);

        b.HasOne<UserAccount>()
            .WithMany()
            .HasForeignKey(x => x.OwnerUserId)
            .OnDelete(DeleteBehavior.NoAction);

        b.HasOne<SampleVoucherOfferRow>()
            .WithMany()
            .HasForeignKey(x => x.OfferId)
            .OnDelete(DeleteBehavior.NoAction);

        b.HasAlternateKey(x => new { x.Id, x.WalletId })
            .HasName("AK_VoucherRequests_Id_Wallet");

        b.HasIndex(x => new
        {
            x.WalletId,
                x.Status
        }).HasDatabaseName("IX_VoucherRequests_Wallet_Status");
    }
}

public sealed class SampleVoucherInventoryRowConfiguration
    : IEntityTypeConfiguration<SampleVoucherInventoryRow>
{
    public void Configure(
        EntityTypeBuilder<SampleVoucherInventoryRow> b)
    {
        b.ToTable("SampleVoucherInventory", t =>
        {
            t.HasCheckConstraint(
                "CK_VoucherInventory_Expiry",
                "[ExpiresAtUtc] > [AddedAtUtc]");
        });

        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();

        b.Property(x => x.CodeHash)
            .HasMaxLength(128).IsRequired();

        b.Property(x => x.EncryptedCodeReference)
            .HasMaxLength(500).IsRequired();

        b.Property(x => x.SampleImageReference)
            .HasMaxLength(2048).IsRequired();

        b.Property(x => x.AddedByAdminUserId)
            .HasMaxLength(256).IsRequired();

        b.Property(x => x.ExpiresAtUtc)
            .HasColumnType("datetimeoffset");

        b.Property(x => x.AddedAtUtc)
            .HasColumnType("datetimeoffset");

        b.Property(x => x.RowVersion).IsRowVersion();

        b.HasOne<SampleVoucherOfferRow>()
            .WithMany()
            .HasForeignKey(x => x.OfferId)
            .OnDelete(DeleteBehavior.NoAction);

        b.HasOne<SampleVoucherRedemptionRequestRow>()
            .WithMany()
            .HasForeignKey(x => x.AssignedRequestId)
            .OnDelete(DeleteBehavior.NoAction);

        b.HasIndex(x => x.CodeHash)
            .IsUnique()
            .HasDatabaseName("UX_VoucherInventory_CodeHash");

        b.HasIndex(x => x.AssignedRequestId)
            .IsUnique()
            .HasFilter("[AssignedRequestId] IS NOT NULL")
            .HasDatabaseName("UX_VoucherInventory_Assignment");
    }
}

public sealed class SampleVoucherIssuanceRowConfiguration
    : IEntityTypeConfiguration<SampleVoucherIssuanceRow>
{
    public void Configure(
        EntityTypeBuilder<SampleVoucherIssuanceRow> b)
    {
        b.ToTable("SampleVoucherIssuances", t =>
        {
            t.HasCheckConstraint(
                "CK_VoucherIssuances_Status",
                "[Status] IN (1, 2, 3, 4, 5)");

            t.HasCheckConstraint(
                "CK_VoucherIssuances_Attempts",
                "[AttemptCount] >= 0");
        });

        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();

        b.Property(x => x.Status)
            .HasConversion<int>().IsRequired();

        b.Property(x => x.QrPayload)
            .HasMaxLength(2048);

        b.Property(x => x.ImageReference)
            .HasMaxLength(2048);

        b.Property(x => x.IssuedAtUtc)
            .HasColumnType("datetimeoffset");

        b.Property(x => x.ExpiresAtUtc)
            .HasColumnType("datetimeoffset");

        b.Property(x => x.UsedAtUtc)
            .HasColumnType("datetimeoffset");

        b.Property(x => x.LastErrorCode)
            .HasMaxLength(128);

        b.Property(x => x.UsedByActorUserId)
            .HasMaxLength(256);
        b.Property(x => x.UseReason)
            .HasMaxLength(1000);

        b.Property(x => x.RowVersion).IsRowVersion();

        b.HasOne<SampleVoucherRedemptionRequestRow>()
            .WithMany()
            .HasForeignKey(x => x.RequestId)
            .OnDelete(DeleteBehavior.NoAction);

        b.HasOne<SampleVoucherInventoryRow>()
            .WithMany()
            .HasForeignKey(x => x.InventoryId)
            .OnDelete(DeleteBehavior.NoAction);

        b.HasIndex(x => x.RequestId)
            .IsUnique()
            .HasDatabaseName("UX_VoucherIssuances_Request");

        b.HasIndex(x => x.InventoryId)
            .IsUnique()
            .HasFilter("[InventoryId] IS NOT NULL")
            .HasDatabaseName("UX_VoucherIssuances_Inventory");
    }
}

public sealed class CoinRewardOutboxRowConfiguration
    : IEntityTypeConfiguration<CoinRewardOutboxRow>
{
    public void Configure(
        EntityTypeBuilder<CoinRewardOutboxRow> b)
    {
        b.ToTable("CoinRewardOutbox", t =>
        {
            t.HasCheckConstraint(
                "CK_CoinRewardOutbox_Amount",
                "[CoinsAmount] > 0");

            t.HasCheckConstraint(
                "CK_CoinRewardOutbox_Attempts",
                "[AttemptNumber] >= 0");

            t.HasCheckConstraint(
                "CK_CoinRewardOutbox_Status",
                "[Status] IN (1, 2, 3)");
        });

        b.HasKey(x => x.EventId);
        b.Property(x => x.EventId).ValueGeneratedNever();

        b.Property(x => x.RewardType)
            .HasConversion<int>().IsRequired();

        b.Property(x => x.SourceRecordId)
            .HasMaxLength(256).IsRequired();

        b.Property(x => x.RecipientUserId)
            .HasMaxLength(256).IsRequired();

        b.Property(x => x.RuleVersion)
            .HasMaxLength(64).IsRequired();

        b.Property(x => x.ApprovalRecordId)
            .HasMaxLength(256);

        b.Property(x => x.GameAttemptId)
            .HasMaxLength(256);

        b.Property(x => x.Status)
            .HasConversion<int>().IsRequired();

        b.Property(x => x.EligibilityConfirmedAtUtc)
            .HasColumnType("datetimeoffset");

        b.Property(x => x.NextDueAtUtc)
            .HasColumnType("datetimeoffset");

        b.Property(x => x.LeaseUntilUtc)
            .HasColumnType("datetimeoffset");

        b.Property(x => x.DeliveredAtUtc)
            .HasColumnType("datetimeoffset");

        b.Property(x => x.ClaimToken)
            .HasMaxLength(128);

        b.Property(x => x.LastErrorCode)
            .HasMaxLength(128);

        b.Property(x => x.RowVersion)
            .IsRowVersion();

        // One authoritative event per recipient and source.
        b.HasIndex(x => new
        {
            x.RewardType,
                x.SourceRecordId,
                x.RecipientUserId
        })
            .IsUnique()
            .HasDatabaseName(
                "UX_CoinOutbox_Recipient_RewardSource");

        // Winner reward source is the GameId and must remain
        // globally unique even if a faulty publisher changes recipient.
        b.HasIndex(x => new
        {
            x.RewardType,
            x.SourceRecordId
        })
            .IsUnique()
            .HasFilter($"[RewardType] = {(int)CoinRewardType.GameWinnerApproved}")
            .HasDatabaseName("UX_CoinOutbox_GameWinnerSource");

        b.HasIndex(x => new
        {
            x.Status,
                x.NextDueAtUtc
        })
            .HasDatabaseName("IX_CoinOutbox_Due");

        b.HasIndex(x => x.RecipientUserId);

        b.HasOne<UserAccount>()
            .WithMany()
            .HasForeignKey(x => x.RecipientUserId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}

public sealed class CoinRewardConsumerDeliveryRowConfiguration
    : IEntityTypeConfiguration<CoinRewardConsumerDeliveryRow>
{
    public void Configure(
        EntityTypeBuilder<CoinRewardConsumerDeliveryRow> b)
    {
        b.ToTable("CoinRewardConsumerDeliveries", t =>
        {
            t.HasCheckConstraint(
                "CK_CoinRewardConsumerDeliveries_Attempts",
                "[AttemptCount] >= 0");

            t.HasCheckConstraint(
                "CK_CoinRewardConsumerDeliveries_Status",
                "[Status] IN (1, 2, 3)");
        });

        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();

        b.Property(x => x.ConsumerName)
            .HasMaxLength(100).IsRequired();

        b.Property(x => x.Status)
            .HasConversion<int>().IsRequired();

        b.Property(x => x.DeliveredAtUtc)
            .HasColumnType("datetimeoffset");

        b.Property(x => x.LastErrorCode)
            .HasMaxLength(128);

        b.HasOne<CoinRewardOutboxRow>()
            .WithMany()
            .HasForeignKey(x => x.EventId)
            .OnDelete(DeleteBehavior.NoAction);

        b.HasIndex(x => new
        {
            x.EventId,
                x.ConsumerName
        })
            .IsUnique()
            .HasDatabaseName("UX_CoinConsumer_Event_Name");
    }
}
