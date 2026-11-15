
using Microsoft.EntityFrameworkCore;

using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Wayhunter.Domain.Modules.TravelPlanning.Entities;
using Wayhunter.Domain.Modules.TripSafety.Entities;
using Wayhunter.Domain.Modules.HomeAdsAnalytics.Entities;

using Wayhunter.Infrastructure.Persistence.Models.CoinsWallet;

using Wayhunter.Infrastructure.Persistence.Configurations.IdentitySocial;
using Wayhunter.Infrastructure.Persistence.Configurations.TravelPlanning;
using Wayhunter.Infrastructure.Persistence.Configurations.TripSafety;
using Wayhunter.Infrastructure.Persistence.Configurations.HomeAdsAnalytics;
using Wayhunter.Infrastructure.Persistence.Configurations.CoinsWallet;

namespace Wayhunter.Infrastructure.Persistence.DbContexts;

/// <summary>
/// WAYHUNTER shared EF Core model.
///
/// M1: IdentitySocial
/// M2: TravelPlanning
/// M3: TripSafety foundation
/// M6: CoinsWallet persistence mappings
/// M7: HomeAdsAnalytics
///
/// M4/M5 mappings and remaining M3 mappings are
/// pending their integration review.
///
/// No database migrations are executed here.
/// </summary>
public sealed class WayhunterSharedDbContext : DbContext
{
    public WayhunterSharedDbContext(
        DbContextOptions<WayhunterSharedDbContext> options)
        : base(options)
    {
    }

    // M1 - Identity
    public DbSet<UserAccount> Users
        => Set<UserAccount>();

    public DbSet<UserProfile> UserProfiles
        => Set<UserProfile>();

    public DbSet<IdentityOtpChallenge> IdentityOtpChallenges
        => Set<IdentityOtpChallenge>();

    public DbSet<IdentitySession> IdentitySessions
        => Set<IdentitySession>();

    public DbSet<IdentityConsent> IdentityConsents
        => Set<IdentityConsent>();

    // M2 - Travel Planning
    public DbSet<TripPlan> Trips
        => Set<TripPlan>();

    public DbSet<TripStop> TripStops
        => Set<TripStop>();

    // M3 - Trip Safety
    public DbSet<LiveTripSession> LiveTripSessions
        => Set<LiveTripSession>();

    // M7 - Advertisement
    public DbSet<Advertisement> Advertisements
        => Set<Advertisement>();

    // M6 - Wallet and immutable Ledger
    public DbSet<CoinWalletRow> CoinWallets
        => Set<CoinWalletRow>();

    public DbSet<CoinLedgerEntryRow> CoinLedgerEntries
        => Set<CoinLedgerEntryRow>();

    public DbSet<CoinReservationRow> CoinReservations
        => Set<CoinReservationRow>();

    public DbSet<CoinRuleVersionRow> CoinRuleVersions
        => Set<CoinRuleVersionRow>();

    // M6 - Sample Vouchers
    public DbSet<SampleVoucherOfferRow> SampleVoucherOffers
        => Set<SampleVoucherOfferRow>();

    public DbSet<SampleVoucherRedemptionRequestRow>
        SampleVoucherRedemptionRequests
        => Set<SampleVoucherRedemptionRequestRow>();

    public DbSet<SampleVoucherInventoryRow>
        SampleVoucherInventory
        => Set<SampleVoucherInventoryRow>();

    public DbSet<SampleVoucherIssuanceRow>
        SampleVoucherIssuances
        => Set<SampleVoucherIssuanceRow>();

    // M6 - Reward delivery
    public DbSet<CoinRewardOutboxRow> CoinRewardOutbox
        => Set<CoinRewardOutboxRow>();

    public DbSet<CoinRewardConsumerDeliveryRow>
        CoinRewardConsumerDeliveries
        => Set<CoinRewardConsumerDeliveryRow>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // M1
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

        // M2
        modelBuilder.ApplyConfiguration(
            new TripPlanConfiguration());

        modelBuilder.ApplyConfiguration(
            new TripStopConfiguration());

        // M3
        modelBuilder.ApplyConfiguration(
            new LiveTripSessionConfiguration());

        // M7
        modelBuilder.ApplyConfiguration(
            new AdvertisementConfiguration());

        // M6 Phase 1
        modelBuilder.ApplyConfiguration(
            new CoinWalletRowConfiguration());

        modelBuilder.ApplyConfiguration(
            new CoinLedgerEntryRowConfiguration());

        modelBuilder.ApplyConfiguration(
            new CoinReservationRowConfiguration());

        modelBuilder.ApplyConfiguration(
            new CoinRuleVersionRowConfiguration());

        // M6 Phase 2
        modelBuilder.ApplyConfiguration(
            new SampleVoucherOfferRowConfiguration());

        modelBuilder.ApplyConfiguration(
            new SampleVoucherRedemptionRequestRowConfiguration());

        modelBuilder.ApplyConfiguration(
            new SampleVoucherInventoryRowConfiguration());

        modelBuilder.ApplyConfiguration(
            new SampleVoucherIssuanceRowConfiguration());

        modelBuilder.ApplyConfiguration(
            new CoinRewardOutboxRowConfiguration());

        modelBuilder.ApplyConfiguration(
            new CoinRewardConsumerDeliveryRowConfiguration());

        // M4, M5 and remaining M3 mappings
        // require separate shared integration reviews.
    }
}
