
using Microsoft.EntityFrameworkCore;

// Domain Entities
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Wayhunter.Domain.Modules.TravelPlanning.Entities;
using Wayhunter.Domain.Modules.TripSafety.Entities;
using Wayhunter.Domain.Modules.HomeAdsAnalytics.Entities;
using Wayhunter.Domain.Modules.HunterPlaces.Entities;

// Persistence Models
using Wayhunter.Infrastructure.Persistence.Models.CoinsWallet;
using Wayhunter.Infrastructure.Persistence.Models.HunterPlaces;

// EF Core Configurations
using Wayhunter.Infrastructure.Persistence.Configurations.IdentitySocial;
using Wayhunter.Infrastructure.Persistence.Configurations.TravelPlanning;
using Wayhunter.Infrastructure.Persistence.Configurations.TripSafety;
using Wayhunter.Infrastructure.Persistence.Configurations.HomeAdsAnalytics;
using Wayhunter.Infrastructure.Persistence.Configurations.CoinsWallet;
using Wayhunter.Infrastructure.Persistence.Configurations.HunterPlaces;

namespace Wayhunter.Infrastructure.Persistence.DbContexts;

/// <summary>
/// WAYHUNTER shared EF Core model.
///
/// M1: IdentitySocial
/// M2: TravelPlanning
/// M3: TripSafety foundation
/// M4: HunterPlaces proposed persistence mappings
/// M6: CoinsWallet persistence mappings
/// M7: HomeAdsAnalytics
///
/// M4 mappings require DB Coordinator review.
/// M5 and remaining M3 mappings are pending.
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

    // ==========================================
    // M1 - Identity
    // ==========================================

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

    // ==========================================
    // M2 - Travel Planning
    // ==========================================

    public DbSet<TripPlan> Trips
        => Set<TripPlan>();

    public DbSet<TripStop> TripStops
        => Set<TripStop>();

    // ==========================================
    // M3 - Trip Safety
    // ==========================================

    public DbSet<LiveTripSession> LiveTripSessions
        => Set<LiveTripSession>();

    // ==========================================
    // M4 - Hunter Places
    // ==========================================

    public DbSet<HunterSubmission> HunterSubmissions
        => Set<HunterSubmission>();

    public DbSet<HunterEvidence> HunterEvidence
        => Set<HunterEvidence>();

    public DbSet<HunterReviewDecision> HunterReviewDecisions
        => Set<HunterReviewDecision>();

    public DbSet<HunterCaptureSessionRow> HunterCaptureSessions
        => Set<HunterCaptureSessionRow>();

    // ==========================================
    // M7 - Advertisement
    // ==========================================

    public DbSet<Advertisement> Advertisements
        => Set<Advertisement>();

    // ==========================================
    // M6 - Wallet and Immutable Ledger
    // ==========================================

    public DbSet<CoinWalletRow> CoinWallets
        => Set<CoinWalletRow>();

    public DbSet<CoinLedgerEntryRow> CoinLedgerEntries
        => Set<CoinLedgerEntryRow>();

    public DbSet<CoinReservationRow> CoinReservations
        => Set<CoinReservationRow>();

    public DbSet<CoinRuleVersionRow> CoinRuleVersions
        => Set<CoinRuleVersionRow>();

    // ==========================================
    // M6 - Sample Vouchers
    // ==========================================

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

    // ==========================================
    // M6 - Reward Delivery
    // ==========================================

    public DbSet<CoinRewardOutboxRow> CoinRewardOutbox
        => Set<CoinRewardOutboxRow>();

    public DbSet<CoinRewardConsumerDeliveryRow>
        CoinRewardConsumerDeliveries
        => Set<CoinRewardConsumerDeliveryRow>();

    // ==========================================
    // EF Core Model Configurations
    // ==========================================

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ======================================
        // M1 - Identity
        // ======================================

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

        // ======================================
        // M2 - Travel Planning
        // ======================================

        modelBuilder.ApplyConfiguration(
            new TripPlanConfiguration());

        modelBuilder.ApplyConfiguration(
            new TripStopConfiguration());

        // ======================================
        // M3 - Trip Safety
        // ======================================

        modelBuilder.ApplyConfiguration(
            new LiveTripSessionConfiguration());

        // ======================================
        // M4 - Hunter Places
        // Proposed mappings, review required.
        // ======================================

        modelBuilder.ApplyConfiguration(
            new HunterSubmissionConfiguration());

        modelBuilder.ApplyConfiguration(
            new HunterEvidenceConfiguration());

        modelBuilder.ApplyConfiguration(
            new HunterReviewDecisionConfiguration());

        modelBuilder.ApplyConfiguration(
            new HunterCaptureSessionRowConfiguration());

        // ======================================
        // M7 - Advertisement
        // ======================================

        modelBuilder.ApplyConfiguration(
            new AdvertisementConfiguration());

        // ======================================
        // M6 - Wallet Phase 1
        // ======================================

        modelBuilder.ApplyConfiguration(
            new CoinWalletRowConfiguration());

        modelBuilder.ApplyConfiguration(
            new CoinLedgerEntryRowConfiguration());

        modelBuilder.ApplyConfiguration(
            new CoinReservationRowConfiguration());

        modelBuilder.ApplyConfiguration(
            new CoinRuleVersionRowConfiguration());

        // ======================================
        // M6 - Voucher Phase 2
        // ======================================

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

        // M5 and remaining M3 mappings
        // require separate shared integration reviews.
    }
}
