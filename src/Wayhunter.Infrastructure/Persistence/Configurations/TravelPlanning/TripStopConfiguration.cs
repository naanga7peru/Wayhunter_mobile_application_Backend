
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wayhunter.Domain.Modules.TravelPlanning.Entities;

namespace Wayhunter.Infrastructure.Persistence.Configurations.TravelPlanning;

public sealed class TripStopConfiguration
    : IEntityTypeConfiguration<TripStop>
{
    public void Configure(EntityTypeBuilder<TripStop> builder)
    {
        // Database table
        builder.ToTable("TripStops");

        // Primary key
        builder.HasKey(stop => stop.Id);

        builder.Property(stop => stop.Id)
            .ValueGeneratedNever();

        // Trip foreign key
        builder.Property(stop => stop.TripPlanId)
            .IsRequired();

        // Stop order
        builder.Property(stop => stop.OrderIndex)
            .IsRequired();

        // Unique stop order within each trip
        builder.HasIndex(stop => new
        {
            stop.TripPlanId,
            stop.OrderIndex
        }).IsUnique();

        // Stop name
        builder.Property(stop => stop.Name)
            .HasMaxLength(200)
            .IsRequired();

        // External or approved Place ID
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

        // TripPlan relationship is already defined
        // in TripPlanConfiguration.cs.
    }
}
