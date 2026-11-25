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
        {
            table.HasCheckConstraint("CK_TripPlanCopies_NotOwnSource", "[TripPlanId] <> [SourceTripPlanId]");
            table.HasCheckConstraint("CK_TripPlanCopies_RequestIdentity",
                "([RequesterUserId] IS NULL AND [RequestKeyHash] IS NULL) OR " +
                "([RequesterUserId] IS NOT NULL AND [RequestKeyHash] IS NOT NULL AND LEN([RequestKeyHash]) = 64)");
        });

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
        builder.Property(row => row.RequesterUserId).HasMaxLength(256);
        builder.Property(row => row.RequestKeyHash).HasMaxLength(64).IsUnicode(false);
        builder.HasIndex(row => new { row.RequesterUserId, row.RequestKeyHash })
            .IsUnique().HasFilter("[RequesterUserId] IS NOT NULL AND [RequestKeyHash] IS NOT NULL");
        builder.HasOne<Wayhunter.Domain.Modules.IdentitySocial.Entities.UserAccount>()
            .WithMany().HasForeignKey(row => row.RequesterUserId).OnDelete(DeleteBehavior.NoAction);
    }
}
