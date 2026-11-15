
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wayhunter.Domain.Modules.TravelPlanning.Entities;
using Wayhunter.Domain.Modules.TripSafety.Entities;

namespace Wayhunter.Infrastructure.Persistence.Configurations.TripSafety;

public sealed class LiveTripSessionConfiguration
    : IEntityTypeConfiguration<LiveTripSession>
{
    public void Configure(
        EntityTypeBuilder<LiveTripSession> builder)
    {
        builder.ToTable(
            "TripSafety_LiveTripSessions",
            table => table.HasCheckConstraint(
                "CK_TripSafety_LiveTripSessions_Status",
                "[Status] IN (1, 2, 3)"));

        // Primary key
        builder.HasKey(x => x.SessionId);

        builder.Property(x => x.SessionId)
            .ValueGeneratedNever();

        // M2 TripPlan.Id and M3 TripPlanId use Guid.
        builder.Property(x => x.TripPlanId)
            .HasColumnType("uniqueidentifier")
            .IsRequired();

        // Session status
        builder.Property(x => x.Status)
            .HasConversion<int>()
            .IsRequired();

        // Start timestamp
        builder.Property(x => x.StartedAtUtc)
            .HasColumnType("datetimeoffset")
            .IsRequired();

        // Finish timestamp
        builder.Property(x => x.FinishedAtUtc)
            .HasColumnType("datetimeoffset")
            .IsRequired(false);

        // M2/M3 foreign key relationship.
        //
        // Principal: Trips.Id
        // Dependent: TripSafety_LiveTripSessions.TripPlanId
        //
        // One TripPlan can have multiple historical
        // LiveTripSessions.
        //
        // NoAction is proposed and requires
        // M2 / DB Coordinator approval.

        builder.HasOne<TripPlan>()
            .WithMany()
            .HasForeignKey(x => x.TripPlanId)
            .HasPrincipalKey(x => x.Id)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName(
                "FK_TripSafety_LiveTripSessions_Trips_TripPlanId");

        // Only one Active or Paused session
        // is allowed per TripPlanId.

        builder.HasIndex(x => x.TripPlanId)
            .IsUnique()
            .HasDatabaseName(
                "UX_TripSafety_LiveTripSessions_OpenPlan")
            .HasFilter("[Status] IN (1, 2)");
    }
}
