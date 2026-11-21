
using Microsoft.EntityFrameworkCore;

// Domain Entities
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Wayhunter.Domain.Modules.TravelPlanning.Entities;
using Wayhunter.Domain.Modules.TripSafety.Entities;
using Wayhunter.Domain.Modules.HomeAdsAnalytics.Entities;
using Wayhunter.Domain.Modules.HunterPlaces.Entities;

// Persistence Models
using Wayhunter.Infrastructure.Persistence.Models.IdentitySocial;
using Wayhunter.Infrastructure.Persistence.Models.CoinsWallet;
using Wayhunter.Infrastructure.Persistence.Models.Games;
using Wayhunter.Infrastructure.Persistence.Models.HunterPlaces;
using Wayhunter.Infrastructure.Persistence.Models.TripSafety;

// EF Core Configurations
using Wayhunter.Infrastructure.Persistence.Configurations.IdentitySocial;
using Wayhunter.Infrastructure.Persistence.Configurations.TravelPlanning;
using Wayhunter.Infrastructure.Persistence.Configurations.TripSafety;
using Wayhunter.Infrastructure.Persistence.Configurations.HomeAdsAnalytics;
using Wayhunter.Infrastructure.Persistence.Configurations.CoinsWallet;
using Wayhunter.Infrastructure.Persistence.Configurations.Games;
using Wayhunter.Infrastructure.Persistence.Configurations.HunterPlaces;

namespace Wayhunter.Infrastructure.Persistence.DbContexts;

/// <summary>
/// WAYHUNTER shared EF Core model.
///
/// M1: IdentitySocial
/// M2: TravelPlanning
/// M3: TripSafety
/// M4: HunterPlaces proposed persistence mappings
/// M5: Games persistence mappings
/// M6: CoinsWallet persistence mappings
/// M7: HomeAdsAnalytics
///
/// M3 configurations in this context are model
/// mappings only. Shared schema and migration
/// approval remain separate.
///
/// No migration is generated or executed here.
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

    public DbSet<IdentityPasswordResetCredential>
        IdentityPasswordResetCredentials
        => Set<IdentityPasswordResetCredential>();

    public DbSet<IdentitySession> IdentitySessions
        => Set<IdentitySession>();

    public DbSet<IdentityConsent> IdentityConsents
        => Set<IdentityConsent>();

    // M1 Administrator role storage.
    // EF mapping only; runtime activation is disabled.
    public DbSet<UserRoleAssignmentRow> UserRoleAssignments
        => Set<UserRoleAssignmentRow>();

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

    public DbSet<TripInvitation> TripInvitations
        => Set<TripInvitation>();

    public DbSet<TripMember> TripMembers
        => Set<TripMember>();

    // M3 - Membership Revocation Outbox
    public DbSet<TripMembershipRevocationOutboxRow>
        TripMembershipRevocationOutbox
        => Set<TripMembershipRevocationOutboxRow>();

    // Proposed M3 mappings only. Runtime activation and migration approval are separate.
    public DbSet<TripSafeCheckInRow> TripSafeCheckIns
        => Set<TripSafeCheckInRow>();

    public DbSet<TripSafeCheckInOutboxRow> TripSafeCheckInOutbox
        => Set<TripSafeCheckInOutboxRow>();

    // Proposed mappings only. No Trip Memories SQL store is registered.
    public DbSet<TripMemoryRow> TripMemories => Set<TripMemoryRow>();

    public DbSet<TripMemoryMediaRow> TripMemoryMedia => Set<TripMemoryMediaRow>();

    // Proposed MS08 mappings only.
    public DbSet<NotificationEventRow> NotificationEvents => Set<NotificationEventRow>();
    public DbSet<NotificationRecipientRow> NotificationRecipients => Set<NotificationRecipientRow>();
    public DbSet<NotificationPreferenceRow> NotificationPreferences => Set<NotificationPreferenceRow>();
    public DbSet<NotificationDeviceRow> NotificationDevices => Set<NotificationDeviceRow>();
    public DbSet<NotificationTokenBindingRow> NotificationTokenBindings => Set<NotificationTokenBindingRow>();
    public DbSet<NotificationJobRow> NotificationJobs => Set<NotificationJobRow>();
    public DbSet<NotificationJobRecipientRow> NotificationJobRecipients => Set<NotificationJobRecipientRow>();
    public DbSet<NotificationAudienceReceiptRow> NotificationAudienceReceipts => Set<NotificationAudienceReceiptRow>();
    public DbSet<NotificationOperationRow> NotificationOperations => Set<NotificationOperationRow>();
    public DbSet<NotificationDeliveryAuditRow> NotificationDeliveryAudit => Set<NotificationDeliveryAuditRow>();

    public DbSet<TripGpsConsent> TripGpsConsents
        => Set<TripGpsConsent>();

    public DbSet<HazardReport> HazardReports
        => Set<HazardReport>();

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
    // M5 - Games
    // ==========================================

    public DbSet<GameRow> Games
        => Set<GameRow>();

    public DbSet<GameStepRow> GameSteps
        => Set<GameStepRow>();

    public DbSet<GameHintRow> GameHints
        => Set<GameHintRow>();

    public DbSet<GameAttemptRow> GameAttempts
        => Set<GameAttemptRow>();

    public DbSet<GameAttemptProgressRow> GameAttemptProgress
        => Set<GameAttemptProgressRow>();

    public DbSet<GameProofEvidenceRow> GameProofEvidence
        => Set<GameProofEvidenceRow>();

    public DbSet<GameCompletionResultRow> GameCompletionResults
        => Set<GameCompletionResultRow>();

    public DbSet<GameCompletionEvidenceRow> GameCompletionEvidence
        => Set<GameCompletionEvidenceRow>();

    public DbSet<GameWinnerRow> GameWinners
        => Set<GameWinnerRow>();

    public DbSet<GameCreationApprovalRow> GameCreationApprovals
        => Set<GameCreationApprovalRow>();

    public DbSet<GameModerationDecisionRow> GameModerationDecisions
        => Set<GameModerationDecisionRow>();

    public DbSet<GameProofReviewDecisionRow> GameProofReviewDecisions
        => Set<GameProofReviewDecisionRow>();

    public DbSet<GameResultDecisionRow> GameResultDecisions
        => Set<GameResultDecisionRow>();

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

    public DbSet<SampleVoucherInventoryRow> SampleVoucherInventory
        => Set<SampleVoucherInventoryRow>();

    public DbSet<SampleVoucherIssuanceRow> SampleVoucherIssuances
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
            new IdentityPasswordResetCredentialConfiguration());

        modelBuilder.ApplyConfiguration(
            new IdentitySessionConfiguration());

        modelBuilder.ApplyConfiguration(
            new IdentityConsentConfiguration());

        // Approved M1 Admin role logical mapping.
        // No shared migration or production role activation.
        modelBuilder.ApplyConfiguration(
            new UserRoleAssignmentConfiguration());

        // ======================================
        // M2 - Travel Planning
        // ======================================

        modelBuilder.ApplyConfiguration(
            new TripPlanConfiguration());

        modelBuilder.ApplyConfiguration(
            new TripStopConfiguration());

        modelBuilder.ApplyConfiguration(
            new TripPlanCopyAttributionConfiguration());

        // ======================================
        // M3 - Trip Safety
        // ======================================

        modelBuilder.ApplyConfiguration(
            new LiveTripSessionConfiguration());

        modelBuilder.ApplyConfiguration(
            new TripInvitationConfiguration());

        modelBuilder.ApplyConfiguration(
            new TripMemberConfiguration());

        // M3 - Membership Revocation Outbox
        modelBuilder.ApplyConfiguration(
            new TripMembershipRevocationOutboxConfiguration());

        modelBuilder.ApplyConfiguration(
            new TripSafeCheckInConfiguration());

        modelBuilder.ApplyConfiguration(
            new TripSafeCheckInOutboxConfiguration());

        modelBuilder.ApplyConfiguration(new TripMemoryConfiguration());
        modelBuilder.ApplyConfiguration(new TripMemoryMediaConfiguration());

        var notifications = new NotificationModelConfiguration();
        modelBuilder.ApplyConfiguration<NotificationEventRow>(notifications);
        modelBuilder.ApplyConfiguration<NotificationRecipientRow>(notifications);
        modelBuilder.ApplyConfiguration<NotificationPreferenceRow>(notifications);
        modelBuilder.ApplyConfiguration<NotificationDeviceRow>(notifications);
        modelBuilder.ApplyConfiguration<NotificationTokenBindingRow>(notifications);
        modelBuilder.ApplyConfiguration<NotificationJobRow>(notifications);
        modelBuilder.ApplyConfiguration<NotificationJobRecipientRow>(notifications);
        modelBuilder.ApplyConfiguration<NotificationAudienceReceiptRow>(notifications);
        modelBuilder.ApplyConfiguration<NotificationOperationRow>(notifications);
        modelBuilder.ApplyConfiguration<NotificationDeliveryAuditRow>(notifications);

        modelBuilder.ApplyConfiguration(
            new TripGpsConsentConfiguration());

        modelBuilder.ApplyConfiguration(
            new HazardReportConfiguration());

        // ======================================
        // M4 - Hunter Places
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
        // M5 - Games
        // ======================================

        modelBuilder.ApplyConfiguration(
            new GameRowConfiguration());

        modelBuilder.ApplyConfiguration(
            new GameStepRowConfiguration());

        modelBuilder.ApplyConfiguration(
            new GameHintRowConfiguration());

        modelBuilder.ApplyConfiguration(
            new GameAttemptRowConfiguration());

        modelBuilder.ApplyConfiguration(
            new GameAttemptProgressRowConfiguration());

        modelBuilder.ApplyConfiguration(
            new GameProofEvidenceRowConfiguration());

        modelBuilder.ApplyConfiguration(
            new GameCompletionResultRowConfiguration());

        modelBuilder.ApplyConfiguration(
            new GameCompletionEvidenceRowConfiguration());

        modelBuilder.ApplyConfiguration(
            new GameWinnerRowConfiguration());

        modelBuilder.ApplyConfiguration(
            new GameCreationApprovalRowConfiguration());

        modelBuilder.ApplyConfiguration(
            new GameModerationDecisionRowConfiguration());

        modelBuilder.ApplyConfiguration(
            new GameProofReviewDecisionRowConfiguration());

        modelBuilder.ApplyConfiguration(
            new GameResultDecisionRowConfiguration());

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
    }
}
