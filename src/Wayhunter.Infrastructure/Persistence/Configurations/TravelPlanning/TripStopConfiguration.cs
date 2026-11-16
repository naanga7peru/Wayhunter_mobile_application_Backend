
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wayhunter.Domain.Modules.TravelPlanning.Entities;

namespace Wayhunter.Infrastructure.Persistence.Configurations.TravelPlanning;

public sealed class TripStopConfiguration
    : IEntityTypeConfiguration<TripStop>
{
    public void Configure(
        EntityTypeBuilder<TripStop> builder)
    {
        // Database table and proposed SQL constraints.
        // Requires DB Coordinator schema approval.
        builder.ToTable("TripStops", table =>
        {
            // Stop order must not be negative.
            table.HasCheckConstraint(
                "CK_TripStops_OrderIndex",
                "[OrderIndex] >= 0");

            // Coordinates must be a valid pair
            // or both must be NULL.
            table.HasCheckConstraint(
                "CK_TripStops_Coordinates",
                "([Latitude] IS NULL AND " +
                "[Longitude] IS NULL) OR " +
                "([Latitude] IS NOT NULL AND " +
                "[Longitude] IS NOT NULL AND " +
                "[Latitude] BETWEEN -90 AND 90 AND " +
                "[Longitude] BETWEEN -180 AND 180)");
        });

        // Primary Key
        builder.HasKey(stop => stop.Id);

        builder.Property(stop => stop.Id)
            .ValueGeneratedNever();

        // M2 TripPlan Foreign Key
        builder.Property(stop => stop.TripPlanId)
            .IsRequired();

        // Ordered stops
        builder.Property(stop => stop.OrderIndex)
            .IsRequired();

        // One stop order per trip.
        builder.HasIndex(stop => new
        {
            stop.TripPlanId,
            stop.OrderIndex
        })
        .IsUnique();

        // Stop name
        builder.Property(stop => stop.Name)
            .HasMaxLength(200)
            .IsRequired();

        // Optional external Place reference
        builder.Property(stop => stop.PlaceId)
            .HasMaxLength(256)
            .IsRequired(false);

        // Optional GPS coordinates
        builder.Property(stop => stop.Latitude)
            .HasColumnType("float")
            .IsRequired(false);

        builder.Property(stop => stop.Longitude)
            .HasColumnType("float")
            .IsRequired(false);

        // TripPlan -> TripStops relationship
        // is configured in TripPlanConfiguration.cs.
    }
}
