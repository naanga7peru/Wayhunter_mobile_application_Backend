using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Wayhunter.Infrastructure.Persistence.Models.TripSafety;

namespace Wayhunter.Infrastructure.Persistence.Configurations.TripSafety;

public sealed class TripSafeCheckInOutboxConfiguration : IEntityTypeConfiguration<TripSafeCheckInOutboxRow>
{
    private const string PendingFilter = "[IsDelivered] = 0 AND [DeadLetteredAtUtc] IS NULL";

    public void Configure(EntityTypeBuilder<TripSafeCheckInOutboxRow> builder)
    {
        builder.ToTable("TripSafety_SafeCheckInOutbox", table =>
        {
            table.HasCheckConstraint("CK_TripSafety_SafeCheckInOutbox_Attempts", "[DeliveryAttempts] >= 0");
            table.HasCheckConstraint("CK_TripSafety_SafeCheckInOutbox_DeliveryState",
                "([IsDelivered] = 0 AND [DeliveredAtUtc] IS NULL) OR " +
                "([IsDelivered] = 1 AND [DeliveredAtUtc] IS NOT NULL AND [DeadLetteredAtUtc] IS NULL)");
            table.HasCheckConstraint("CK_TripSafety_SafeCheckInOutbox_LeasePair",
                "([LeaseToken] IS NULL AND [LeaseUntilUtc] IS NULL) OR " +
                "([LeaseToken] IS NOT NULL AND [LeaseUntilUtc] IS NOT NULL)");
            table.HasCheckConstraint("CK_TripSafety_SafeCheckInOutbox_TerminalState",
                "([IsDelivered] = 0 AND [DeadLetteredAtUtc] IS NULL) OR " +
                "([LeaseToken] IS NULL AND [LeaseUntilUtc] IS NULL AND [NextAttemptAtUtc] IS NULL)");
        });

        builder.HasKey(x => x.Id).HasName("PK_TripSafety_SafeCheckInOutbox");
        builder.Property(x => x.Id).HasColumnType("uniqueidentifier").ValueGeneratedNever().IsRequired();
        builder.Property(x => x.CheckInId).HasColumnType("uniqueidentifier").IsRequired();
        builder.Property(x => x.RecipientUserId).HasColumnType("nvarchar(256)").HasMaxLength(256).IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasColumnType("datetimeoffset(7)").IsRequired();
        builder.Property(x => x.DeliveryAttempts).HasColumnType("int").HasDefaultValue(0).IsRequired();
        builder.Property(x => x.IsDelivered).HasColumnType("bit").HasDefaultValue(false).IsRequired();
        builder.Property(x => x.DeliveredAtUtc).HasColumnType("datetimeoffset(7)").IsRequired(false);
        builder.Property(x => x.NextAttemptAtUtc).HasColumnType("datetimeoffset(7)").IsRequired(false);
        builder.Property(x => x.LeaseToken).HasColumnType("uniqueidentifier").IsRequired(false);
        builder.Property(x => x.LeaseUntilUtc).HasColumnType("datetimeoffset(7)").IsRequired(false);
        builder.Property(x => x.DeadLetteredAtUtc).HasColumnType("datetimeoffset(7)").IsRequired(false);
        builder.Property(x => x.LastError).HasColumnType("nvarchar(256)").HasMaxLength(256).IsRequired(false);

        builder.HasIndex(x => x.CheckInId).IsUnique().HasDatabaseName("UX_TripSafety_SafeCheckInOutbox_CheckInId");
        builder.HasIndex(x => x.RecipientUserId).HasDatabaseName("IX_TripSafety_SafeCheckInOutbox_RecipientUserId");
        builder.HasIndex(x => new { x.NextAttemptAtUtc, x.CreatedAtUtc, x.Id }).HasFilter(PendingFilter)
            .HasDatabaseName("IX_TripSafety_SafeCheckInOutbox_PendingDue");
        builder.HasIndex(x => new { x.LeaseUntilUtc, x.Id }).HasFilter(PendingFilter + " AND [LeaseUntilUtc] IS NOT NULL")
            .HasDatabaseName("IX_TripSafety_SafeCheckInOutbox_PendingLeaseExpiry");

        builder.HasOne<TripSafeCheckInRow>().WithOne().HasForeignKey<TripSafeCheckInOutboxRow>(x => x.CheckInId)
            .OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_TripSafety_SafeCheckInOutbox_SafeCheckIns_CheckInId");
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.RecipientUserId)
            .OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_TripSafety_SafeCheckInOutbox_Users_RecipientUserId");
    }
}
