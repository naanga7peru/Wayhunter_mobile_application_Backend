using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wayhunter.Application.Modules.TripSafety.Memories;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Wayhunter.Domain.Modules.TravelPlanning.Entities;
using Wayhunter.Infrastructure.Persistence.Models.TripSafety;

namespace Wayhunter.Infrastructure.Persistence.Configurations.TripSafety;

public sealed class TripMemoryConfiguration : IEntityTypeConfiguration<TripMemoryRow>
{
    public void Configure(EntityTypeBuilder<TripMemoryRow> builder)
    {
        builder.ToTable("TripSafety_TripMemories", table =>
        {
            table.HasCheckConstraint("CK_TripSafety_TripMemories_Visibility", "[Visibility] IN (0, 1)");
            table.HasCheckConstraint("CK_TripSafety_TripMemories_Timestamps",
                "[UpdatedAtUtc] >= [CreatedAtUtc] AND ([DeletedAtUtc] IS NULL OR [DeletedAtUtc] >= [CreatedAtUtc])");
        });
        builder.HasKey(x => x.MemoryId);
        builder.Property(x => x.MemoryId).ValueGeneratedNever();
        builder.Property(x => x.OwnerUserId).HasMaxLength(256).IsRequired();
        builder.Property(x => x.Caption).HasMaxLength(TripMemoryService.CaptionLimit).IsRequired();
        builder.Property(x => x.Visibility).HasConversion<int>().HasDefaultValue(TripMemoryVisibility.Private);
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.HasIndex(x => new { x.TripPlanId, x.OwnerUserId, x.CreateOperationId }).IsUnique();
        builder.HasIndex(x => new { x.TripPlanId, x.OwnerUserId, x.CreatedAtUtc })
            .HasFilter("[DeletedAtUtc] IS NULL");

        builder.HasOne<TripPlan>().WithMany().HasForeignKey(x => x.TripPlanId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.OwnerUserId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<TripStop>().WithMany()
            .HasForeignKey(x => new { x.TripStopId, x.TripPlanId })
            .HasPrincipalKey(x => new { x.Id, x.TripPlanId })
            // Keep the optional link on tracked memories, including tombstones.
            // SQL Server still receives NO ACTION.
            .OnDelete(DeleteBehavior.ClientNoAction);
    }
}

public sealed class TripMemoryMediaConfiguration : IEntityTypeConfiguration<TripMemoryMediaRow>
{
    public void Configure(EntityTypeBuilder<TripMemoryMediaRow> builder)
    {
        builder.ToTable("TripSafety_TripMemoryMedia", table =>
        {
            table.HasCheckConstraint("CK_TripSafety_TripMemoryMedia_Size", "[ContentLength] > 0");
            table.HasCheckConstraint("CK_TripSafety_TripMemoryMedia_State",
                "([State] = 0 AND [ObjectKey] IS NULL AND [ObjectVersion] IS NULL AND [CleanupClaimId] IS NULL AND [CleanedAtUtc] IS NULL) OR " +
                "([State] = 1 AND [ObjectKey] IS NOT NULL AND [ObjectVersion] IS NOT NULL AND [CleanupClaimId] IS NULL AND [CleanedAtUtc] IS NULL) OR " +
                "([State] IN (2, 3) AND [ObjectKey] IS NOT NULL AND [ObjectVersion] IS NOT NULL AND [CleanupClaimId] IS NOT NULL AND " +
                "(([State] = 2 AND [CleanedAtUtc] IS NULL) OR ([State] = 3 AND [CleanedAtUtc] IS NOT NULL)))");
        });
        builder.HasKey(x => x.OperationId);
        builder.Property(x => x.OperationId).ValueGeneratedNever();
        builder.Property(x => x.ContentType).HasMaxLength(64).IsRequired();
        builder.Property(x => x.ObjectKey).HasMaxLength(450);
        builder.Property(x => x.ObjectVersion).HasMaxLength(256);
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.HasIndex(x => x.MemoryId).IsUnique();
        builder.HasIndex(x => x.ObjectKey).IsUnique().HasFilter("[ObjectKey] IS NOT NULL");
        builder.HasIndex(x => new { x.State, x.CreatedAtUtc });
        builder.HasOne<TripMemoryRow>().WithOne().HasForeignKey<TripMemoryMediaRow>(x => x.MemoryId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
