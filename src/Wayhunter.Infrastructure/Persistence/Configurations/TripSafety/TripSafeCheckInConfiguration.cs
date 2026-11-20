using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Wayhunter.Domain.Modules.TravelPlanning.Entities;
using Wayhunter.Domain.Modules.TripSafety.Entities;
using Wayhunter.Infrastructure.Persistence.Models.TripSafety;

namespace Wayhunter.Infrastructure.Persistence.Configurations.TripSafety;

public sealed class TripSafeCheckInConfiguration : IEntityTypeConfiguration<TripSafeCheckInRow>
{
    public void Configure(EntityTypeBuilder<TripSafeCheckInRow> builder)
    {
        builder.ToTable("TripSafety_SafeCheckIns");

        builder.HasKey(x => x.CheckInId).HasName("PK_TripSafety_SafeCheckIns");
        builder.Property(x => x.CheckInId).HasColumnType("uniqueidentifier").ValueGeneratedNever().IsRequired();
        builder.Property(x => x.TripPlanId).HasColumnType("uniqueidentifier").IsRequired();
        builder.Property(x => x.SessionId).HasColumnType("uniqueidentifier").IsRequired();
        builder.Property(x => x.UserId).HasColumnType("nvarchar(256)").HasMaxLength(256).IsRequired();
        builder.Property(x => x.CheckedInAtUtc).HasColumnType("datetimeoffset(7)").IsRequired();

        builder.HasIndex(x => new { x.SessionId, x.UserId }).IsUnique()
            .HasDatabaseName("UX_TripSafety_SafeCheckIns_SessionId_UserId");
        builder.HasIndex(x => new { x.SessionId, x.TripPlanId })
            .HasDatabaseName("IX_TripSafety_SafeCheckIns_SessionId_TripPlanId");
        builder.HasIndex(x => x.TripPlanId).HasDatabaseName("IX_TripSafety_SafeCheckIns_TripPlanId");
        builder.HasIndex(x => x.UserId).HasDatabaseName("IX_TripSafety_SafeCheckIns_UserId");

        builder.HasOne<TripPlan>().WithMany().HasForeignKey(x => x.TripPlanId)
            .OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_TripSafety_SafeCheckIns_Trips_TripPlanId");
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_TripSafety_SafeCheckIns_Users_UserId");
        // The alternate key is added only to the shared model that includes check-ins.
        builder.HasOne<LiveTripSession>().WithMany()
            .HasForeignKey(x => new { x.SessionId, x.TripPlanId })
            .HasPrincipalKey(x => new { x.SessionId, x.TripPlanId })
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_TripSafety_SafeCheckIns_LiveTripSessions_SessionId_TripPlanId");
    }
}
