
using Microsoft.EntityFrameworkCore;

using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Wayhunter.Domain.Modules.TravelPlanning.Entities;
using Wayhunter.Domain.Modules.TripSafety.Entities;
using Wayhunter.Domain.Modules.HomeAdsAnalytics.Entities;

using Wayhunter.Infrastructure.Persistence.Configurations.IdentitySocial;
using Wayhunter.Infrastructure.Persistence.Configurations.TravelPlanning;
using Wayhunter.Infrastructure.Persistence.Configurations.TripSafety;
using Wayhunter.Infrastructure.Persistence.Configurations.HomeAdsAnalytics;

namespace Wayhunter.Infrastructure.Persistence.DbContexts;

/// <summary>
/// Shared EF Core model foundation for WAYHUNTER.
///
/// Phase 1:
/// M1 IdentitySocial
/// M2 TravelPlanning
/// M3 TripSafety (LiveTripSession only)
/// M7 HomeAdsAnalytics
///
/// M4, M5 and M6 mappings are not yet included.
///
/// No migrations or database updates are performed
/// by this class.
/// </summary>
public sealed class WayhunterSharedDbContext : DbContext
{
    public WayhunterSharedDbContext(
        DbContextOptions<WayhunterSharedDbContext> options)
        : base(options)
    {
    }

    // M1
    public DbSet<UserAccount> Users => Set<UserAccount>();

    public DbSet<UserProfile> UserProfiles =>
        Set<UserProfile>();

    public DbSet<IdentityOtpChallenge> IdentityOtpChallenges =>
        Set<IdentityOtpChallenge>();

    public DbSet<IdentitySession> IdentitySessions =>
        Set<IdentitySession>();

    public DbSet<IdentityConsent> IdentityConsents =>
        Set<IdentityConsent>();

    // M2
    public DbSet<TripPlan> Trips => Set<TripPlan>();

    public DbSet<TripStop> TripStops => Set<TripStop>();

    // M3
    public DbSet<LiveTripSession> LiveTripSessions =>
        Set<LiveTripSession>();

    // M7
    public DbSet<Advertisement> Advertisements =>
        Set<Advertisement>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // M1 Identity
        modelBuilder.ApplyConfiguration(
            new UserAccountConfiguration());

        modelBuilder.ApplyConfiguration(
            new UserProfileConfiguration());

        modelBuilder.ApplyConfiguration(
            new IdentityOtpChallengeConfiguration());

        modelBuilder.ApplyConfiguration(
            new IdentitySessionConfiguration());

        modelBuilder.ApplyConfiguration(
            new IdentityConsentConfiguration());

        // M2 Travel Planning
        modelBuilder.ApplyConfiguration(
            new TripPlanConfiguration());

        modelBuilder.ApplyConfiguration(
            new TripStopConfiguration());

        // M3 Trip Safety
        modelBuilder.ApplyConfiguration(
            new LiveTripSessionConfiguration());

        // M7 Advertisement
        modelBuilder.ApplyConfiguration(
            new AdvertisementConfiguration());

        // M4 / M5 / M6 mappings will be added
        // after their persistence contracts are reviewed.
    }
}
