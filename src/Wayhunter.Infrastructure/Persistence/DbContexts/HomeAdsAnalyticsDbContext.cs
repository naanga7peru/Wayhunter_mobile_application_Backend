
using Microsoft.EntityFrameworkCore;
using Wayhunter.Domain.Modules.HomeAdsAnalytics.Entities;
using Wayhunter.Infrastructure.Persistence.Configurations.HomeAdsAnalytics;

namespace Wayhunter.Infrastructure.Persistence.DbContexts;

public sealed class HomeAdsAnalyticsDbContext : DbContext
{
    public HomeAdsAnalyticsDbContext(
        DbContextOptions<HomeAdsAnalyticsDbContext> options)
        : base(options)
    {
    }

    // Advertisements table
    public DbSet<Advertisement> Advertisements
        => Set<Advertisement>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply advertisement database configuration
        modelBuilder.ApplyConfiguration(
            new AdvertisementConfiguration());
    }
}
