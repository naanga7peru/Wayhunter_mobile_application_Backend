
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Wayhunter.Domain.Modules.TravelPlanning.Entities;

namespace Wayhunter.Infrastructure.Persistence.Configurations.TravelPlanning;

public sealed class TripPlanConfiguration
    : IEntityTypeConfiguration<TripPlan>
{
    public void Configure(EntityTypeBuilder<TripPlan> builder)
    {
        // Table and proposed database constraints.
        // Final schema approval is required.
        builder.ToTable("Trips", table =>
        {
            table.HasCheckConstraint(
                "CK_Trips_DateRange",
                "[EndDate] >= [StartDate]");

            table.HasCheckConstraint(
                "CK_Trips_Budget",
                "[BudgetLkr] >= 0");

            table.HasCheckConstraint(
                "CK_Trips_Headcount",
                "[Headcount] >= 1");
        });

        // Primary Key
        builder.HasKey(trip => trip.Id);

        builder.Property(trip => trip.Id)
            .ValueGeneratedNever();

        // Owner reference
        builder.Property(trip => trip.OwnerUserId)
            .HasMaxLength(256)
            .IsRequired();

        builder.HasIndex(trip => trip.OwnerUserId);

        // M1 -> M2 Foreign Key
        // Users.UserId -> Trips.OwnerUserId
        // Proposed: requires M1 and DB Coordinator approval.
        builder.HasOne<UserAccount>()
            .WithMany()
            .HasForeignKey(trip => trip.OwnerUserId)
            .HasPrincipalKey(user => user.UserId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName(
                "FK_Trips_Users_OwnerUserId");

        // Trip details
        builder.Property(trip => trip.TripName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(trip => trip.StartDate)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(trip => trip.EndDate)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(trip => trip.BudgetLkr)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(trip => trip.VehicleType)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(trip => trip.Headcount)
            .IsRequired();

        // Draft lifecycle status
        builder.Property(trip => trip.DraftStatus)
            .HasConversion<int>()
            .IsRequired();

        // SQL Server optimistic concurrency.
        // Proposed schema change — migration approval pending.
        builder.Property(trip => trip.RowVersion)
            .IsRowVersion()
            .IsConcurrencyToken();


        // M2 TripPlan -> TripStops relationship
        builder.HasMany(trip => trip.Stops)
            .WithOne()
            .HasForeignKey(stop => stop.TripPlanId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(trip => trip.Stops)
            .UsePropertyAccessMode(
                PropertyAccessMode.Field);
    }
}
