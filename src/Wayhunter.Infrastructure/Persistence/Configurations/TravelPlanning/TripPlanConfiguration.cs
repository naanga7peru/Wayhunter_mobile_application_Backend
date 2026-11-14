
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wayhunter.Domain.Modules.TravelPlanning.Entities;

namespace Wayhunter.Infrastructure.Persistence.Configurations.TravelPlanning;

public sealed class TripPlanConfiguration
    : IEntityTypeConfiguration<TripPlan>
{
    public void Configure(EntityTypeBuilder<TripPlan> builder)
    {
        // Table name
        builder.ToTable("Trips");

        // Primary Key
        builder.HasKey(trip => trip.Id);

        builder.Property(trip => trip.Id)
            .ValueGeneratedNever();

        // Owner reference
        builder.Property(trip => trip.OwnerUserId)
            .HasMaxLength(256)
            .IsRequired();

        builder.HasIndex(trip => trip.OwnerUserId);

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
            .HasColumnType("decimal(18,2)");

        builder.Property(trip => trip.VehicleType)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(trip => trip.Headcount)
            .IsRequired();

        // Draft lifecycle status
        builder.Property(trip => trip.DraftStatus)
            .HasConversion<int>()
            .IsRequired();

        // One Trip has many ordered stops
        builder.HasMany(trip => trip.Stops)
            .WithOne()
            .HasForeignKey(stop => stop.TripPlanId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(trip => trip.Stops)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
