
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wayhunter.Domain.Modules.TripSafety.Entities;
using Wayhunter.Infrastructure.Persistence.Models.TripSafety;

namespace Wayhunter.Infrastructure.Persistence.Configurations.TripSafety;

public sealed class TripMembershipRevocationOutboxConfiguration
    : IEntityTypeConfiguration<TripMembershipRevocationOutboxRow>
{
    public void Configure(
        EntityTypeBuilder<TripMembershipRevocationOutboxRow> builder)
    {
        builder.ToTable(
            "TripSafety_MemberRevocationOutbox");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.Property(x => x.TripPlanId)
            .HasColumnType("uniqueidentifier")
            .IsRequired();

        builder.Property(x => x.MemberId)
            .HasColumnType("uniqueidentifier")
            .IsRequired();

        builder.Property(x => x.MemberUserId)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(x => x.RemovedAtUtc)
            .HasColumnType("datetimeoffset")
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnType("datetimeoffset")
            .IsRequired();

        builder.Property(x => x.IsDelivered)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(x => x.DeliveredAtUtc)
            .HasColumnType("datetimeoffset")
            .IsRequired(false);

        builder.Property(x => x.DeliveryAttempts)
            .HasDefaultValue(0)
            .IsRequired();

        builder.HasIndex(x => x.MemberId)
            .IsUnique()
            .HasDatabaseName(
                "UX_TripSafety_MemberRevocationOutbox_MemberId");

        builder.HasIndex(x => new
        {
            x.IsDelivered,
            x.CreatedAtUtc
        })
        .HasDatabaseName(
            "IX_TripSafety_MemberRevocationOutbox_Pending");

        builder.HasOne<TripMember>()
            .WithMany()
            .HasForeignKey(x => x.MemberId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.ToTable(
            "TripSafety_MemberRevocationOutbox",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_TripSafety_MemberRevocationOutbox_Attempts",
                    "[DeliveryAttempts] >= 0");

                table.HasCheckConstraint(
                    "CK_TripSafety_MemberRevocationOutbox_DeliveryState",
                    "([IsDelivered] = 0 AND [DeliveredAtUtc] IS NULL) OR " +
                    "([IsDelivered] = 1 AND [DeliveredAtUtc] IS NOT NULL)");
            });
    }
}
