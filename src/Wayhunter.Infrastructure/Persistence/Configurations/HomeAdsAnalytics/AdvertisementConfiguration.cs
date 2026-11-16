
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wayhunter.Domain.Modules.HomeAdsAnalytics.Entities;

namespace Wayhunter.Infrastructure.Persistence.Configurations.HomeAdsAnalytics;

public sealed class AdvertisementConfiguration
    : IEntityTypeConfiguration<Advertisement>
{
    public void Configure(
        EntityTypeBuilder<Advertisement> builder)
    {
        // SQL Server table and approved CHECK constraints
        builder.ToTable("Advertisements", table =>
        {
            // End date must be later than start date
            table.HasCheckConstraint(
                "CK_Advertisements_ValidWindow",
                "[EndAtUtc] > [StartAtUtc]");

            // HomeBanner = 1, SponsoredPoi = 2
            table.HasCheckConstraint(
                "CK_Advertisements_Placement",
                "[Placement] IN (1, 2)");

            // Draft = 0, Active = 1
            // Paused = 2, Archived = 3
            table.HasCheckConstraint(
                "CK_Advertisements_Status",
                "[Status] IN (0, 1, 2, 3)");
        });

        // Application-generated uniqueidentifier Primary Key
        builder.HasKey(ad => ad.Id)
            .HasName("PK_Advertisements");

        builder.Property(ad => ad.Id)
            .HasColumnType("uniqueidentifier")
            .ValueGeneratedNever();

        // Advertisement Title
        builder.Property(ad => ad.Title)
            .IsRequired()
            .HasMaxLength(200);

        // Advertisement Image URL
        builder.Property(ad => ad.ImageUrl)
            .IsRequired()
            .HasMaxLength(2048);

        // Destination URL (optional)
        builder.Property(ad => ad.DestinationUrl)
            .HasMaxLength(2048);

        // Advertisement Start Date
        builder.Property(ad => ad.StartAtUtc)
            .HasColumnType("datetimeoffset")
            .IsRequired();

        // Advertisement Expiry Date
        builder.Property(ad => ad.EndAtUtc)
            .HasColumnType("datetimeoffset")
            .IsRequired();

        // Enabled / Disabled Status
        builder.Property(ad => ad.IsEnabled)
            .IsRequired();

        // Advertisement Placement
        // HomeBanner = 1, SponsoredPoi = 2
        builder.Property(ad => ad.Placement)
            .HasConversion<int>()
            .IsRequired();

        // Advertisement Lifecycle Status
        // Draft = 0, Active = 1
        // Paused = 2, Archived = 3
        builder.Property(ad => ad.Status)
            .HasConversion<int>()
            .IsRequired();

        // Advertisement Impression Counter
        builder.Property(ad => ad.ImpressionCount)
            .HasColumnType("bigint")
            .HasDefaultValue(0L)
            .IsRequired();

        // Advertisement Click Counter
        builder.Property(ad => ad.ClickCount)
            .HasColumnType("bigint")
            .HasDefaultValue(0L)
            .IsRequired();

        // Optimize active advertisement searches
        builder.HasIndex(ad => new
        {
            ad.IsEnabled,
            ad.StartAtUtc,
            ad.EndAtUtc
        })
        .HasDatabaseName(
            "IX_Advertisements_Enabled_Window");
    }
}
