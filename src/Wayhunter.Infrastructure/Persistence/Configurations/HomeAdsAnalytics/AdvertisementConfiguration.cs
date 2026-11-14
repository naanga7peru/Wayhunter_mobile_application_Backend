
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
        // SQL Server table name
        builder.ToTable("Advertisements", table =>
        {
            table.HasCheckConstraint(
                "CK_Advertisements_ValidWindow",
                "[EndAtUtc] > [StartAtUtc]");
        });

        // Primary Key
        builder.HasKey(ad => ad.Id);

        builder.Property(ad => ad.Id)
            .ValueGeneratedNever();

        // Advertisement Title
        builder.Property(ad => ad.Title)
            .IsRequired()
            .HasMaxLength(200);

        // Advertisement Image URL
        builder.Property(ad => ad.ImageUrl)
            .IsRequired()
            .HasMaxLength(2048);

        // Destination URL
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
        // Draft = 0, Active = 1, Paused = 2, Archived = 3
        builder.Property(ad => ad.Status)
            .HasConversion<int>()
            .IsRequired();


        // Optimize active ad searches
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
