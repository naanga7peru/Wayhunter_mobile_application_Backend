using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wayhunter.Domain.Modules.TravelPlanning.Entities;
using Wayhunter.Infrastructure.Persistence.Models.TravelPlanning;

namespace Wayhunter.Infrastructure.Persistence.Configurations.TravelPlanning;

public sealed class TripPlanCopyAttributionConfiguration
    : IEntityTypeConfiguration<TripPlanCopyAttributionRow>
{
    public void Configure(EntityTypeBuilder<TripPlanCopyAttributionRow> builder)
    {
        builder.ToTable("TripPlanCopyAttributions", table =>
            table.HasCheckConstraint("CK_TripPlanCopies_NotOwnSource",
                "[TripPlanId] <> [SourceTripPlanId]"));

        builder.HasKey(row => row.TripPlanId);
        builder.Property(row => row.TripPlanId).ValueGeneratedNever();
        builder.Property(row => row.SourceTripPlanId).IsRequired();
        builder.Property(row => row.SourceOwnerUserId)
            .HasMaxLength(256).IsRequired();
        builder.Property(row => row.CopiedAtUtc).IsRequired();


        builder.HasOne<TripPlan>()
            .WithMany()
            .HasForeignKey(row => row.TripPlanId)
            .OnDelete(DeleteBehavior.NoAction);


        builder.HasIndex(row => row.SourceTripPlanId);
    }
}
