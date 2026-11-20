using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Wayhunter.Domain.Modules.TravelPlanning.Entities;
using Wayhunter.Domain.Modules.TripSafety.Entities;
using Wayhunter.Infrastructure.Persistence.Models.TripSafety;

namespace Wayhunter.Infrastructure.Persistence.Configurations.TripSafety;

// Proposed model only. The DB Coordinator owns physical schema and deployment.
public sealed class NotificationModelConfiguration :
    IEntityTypeConfiguration<NotificationEventRow>, IEntityTypeConfiguration<NotificationRecipientRow>,
    IEntityTypeConfiguration<NotificationPreferenceRow>, IEntityTypeConfiguration<NotificationDeviceRow>,
    IEntityTypeConfiguration<NotificationTokenBindingRow>, IEntityTypeConfiguration<NotificationJobRow>, IEntityTypeConfiguration<NotificationJobRecipientRow>,
    IEntityTypeConfiguration<NotificationAudienceReceiptRow>, IEntityTypeConfiguration<NotificationOperationRow>,
    IEntityTypeConfiguration<NotificationDeliveryAuditRow>
{
    public void Configure(EntityTypeBuilder<NotificationEventRow> b)
    {
        b.ToTable("TripSafety_NotificationEvents", t =>
            t.HasCheckConstraint("CK_MS08_NotificationEvent_Template", "[Category] BETWEEN 1 AND 7 AND [Template] BETWEEN 1 AND 7 AND [TemplateVersion] > 0"));
        b.HasKey(x => x.EventId);
        b.Property(x => x.EventId).ValueGeneratedNever();
        b.HasIndex(x => x.JobId).IsUnique().HasFilter("[JobId] IS NOT NULL");
        b.HasOne<NotificationJobRow>().WithMany().HasForeignKey(x => x.JobId).OnDelete(DeleteBehavior.NoAction);
    }

    public void Configure(EntityTypeBuilder<NotificationRecipientRow> b)
    {
        b.ToTable("TripSafety_NotificationRecipients", t =>
        {
            t.HasCheckConstraint("CK_MS08_NotificationRecipient_Lease",
                "([LeaseToken] IS NULL AND [LeaseUntilUtc] IS NULL) OR ([State] = 0 AND [LeaseToken] IS NOT NULL AND [LeaseUntilUtc] IS NOT NULL)");
            t.HasCheckConstraint("CK_MS08_NotificationRecipient_State",
                "[Attempts] >= 0 AND (([State] = 0 AND [CompletedAtUtc] IS NULL) OR ([State] BETWEEN 1 AND 3 AND [CompletedAtUtc] IS NOT NULL AND [NextAttemptAtUtc] IS NULL))");
        });
        b.HasKey(x => new { x.EventId, x.UserId });
        b.HasIndex(x => new { x.UserId, x.ReadAtUtc, x.EventId });
        b.HasIndex(x => new { x.NextAttemptAtUtc, x.LeaseUntilUtc }).HasFilter("[State] = 0");
        b.Property(x => x.LastOutcome).HasMaxLength(48);
        b.HasOne<NotificationEventRow>().WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.NoAction);
        User(b, nameof(NotificationRecipientRow.UserId));
    }

    public void Configure(EntityTypeBuilder<NotificationPreferenceRow> b)
    {
        b.ToTable("TripSafety_NotificationPreferences", t =>
            t.HasCheckConstraint("CK_MS08_NotificationPreference_Category", "[Category] BETWEEN 1 AND 7"));
        b.HasKey(x => new { x.UserId, x.Category });
        User(b, nameof(NotificationPreferenceRow.UserId));
    }

    public void Configure(EntityTypeBuilder<NotificationDeviceRow> b)
    {
        b.ToTable("TripSafety_NotificationDevices");
        b.HasKey(x => x.DeviceId);
        b.Property(x => x.DeviceId).ValueGeneratedNever();
        b.Property(x => x.TokenFingerprint).HasMaxLength(64).IsUnicode(false);
        b.Property(x => x.ProtectedToken).HasMaxLength(8192);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasIndex(x => x.TokenFingerprint).IsUnique();
        b.HasIndex(x => new { x.UserId, x.RevokedAtUtc });
        User(b, nameof(NotificationDeviceRow.UserId));
    }

    public void Configure(EntityTypeBuilder<NotificationTokenBindingRow> b)
    {
        b.ToTable("TripSafety_NotificationTokenBindings");
        b.HasKey(x => x.Fingerprint);
        b.Property(x => x.Fingerprint).HasMaxLength(64).IsUnicode(false);
        b.HasOne<NotificationDeviceRow>().WithMany().HasForeignKey(x => x.DeviceId).OnDelete(DeleteBehavior.NoAction);
        User(b, nameof(NotificationTokenBindingRow.UserId));
    }

    public void Configure(EntityTypeBuilder<NotificationJobRow> b)
    {
        b.ToTable("TripSafety_NotificationJobs", t =>
        {
            t.HasCheckConstraint("CK_MS08_NotificationJob_State", "[State] BETWEEN 0 AND 2 AND [Revision] > 0");
            t.HasCheckConstraint("CK_MS08_NotificationJob_Kind",
                "([Kind] = 1 AND [TripPlanId] IS NOT NULL AND [SessionId] IS NOT NULL AND [TripVersion] IS NOT NULL AND [PlannedStartDate] IS NOT NULL) OR " +
                "([Kind] = 2 AND [TripPlanId] IS NULL AND [SessionId] IS NULL AND [TripVersion] IS NULL AND [PlannedStartDate] IS NULL AND [AudienceFingerprint] IS NOT NULL)");
        });
        b.HasKey(x => x.JobId);
        b.Property(x => x.JobId).ValueGeneratedNever();
        b.Property(x => x.RowVersion).IsRowVersion();
        b.Property(x => x.TripVersion).HasMaxLength(16).IsUnicode(false);
        b.Property(x => x.AudienceDistrict).HasMaxLength(40);
        b.Property(x => x.AudienceFingerprint).HasMaxLength(64).IsUnicode(false);
        b.HasIndex(x => new { x.TripPlanId, x.SessionId, x.ActorId }).IsUnique().HasFilter("[Kind] = 1");
        b.HasIndex(x => new { x.State, x.DueAtUtc });
        b.HasOne<TripPlan>().WithMany().HasForeignKey(x => x.TripPlanId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<LiveTripSession>().WithMany().HasForeignKey(x => new { x.SessionId, x.TripPlanId })
            .HasPrincipalKey(x => new { x.SessionId, x.TripPlanId }).OnDelete(DeleteBehavior.NoAction);
        User(b, nameof(NotificationJobRow.ActorId));
    }

    public void Configure(EntityTypeBuilder<NotificationJobRecipientRow> b)
    {
        b.ToTable("TripSafety_NotificationJobRecipients");
        b.HasKey(x => new { x.JobId, x.UserId });
        b.HasOne<NotificationJobRow>().WithMany().HasForeignKey(x => x.JobId).OnDelete(DeleteBehavior.NoAction);
        User(b, nameof(NotificationJobRecipientRow.UserId));
    }

    public void Configure(EntityTypeBuilder<NotificationAudienceReceiptRow> b)
    {
        b.ToTable("TripSafety_NotificationAudienceReceipts", t =>
            t.HasCheckConstraint("CK_MS08_NotificationAudience_Count", "[Count] BETWEEN 1 AND 100"));
        b.HasKey(x => x.ReceiptId);
        b.Property(x => x.ReceiptId).ValueGeneratedNever();
        b.Property(x => x.Fingerprint).HasMaxLength(64).IsUnicode(false);
        b.HasIndex(x => x.ExpiresAtUtc);
        User(b, nameof(NotificationAudienceReceiptRow.ActorId));
    }

    public void Configure(EntityTypeBuilder<NotificationOperationRow> b)
    {
        b.ToTable("TripSafety_NotificationOperations");
        b.HasKey(x => new { x.ActorId, x.OperationId });
        b.Property(x => x.Action).HasMaxLength(32).IsUnicode(false);
        b.Property(x => x.Fingerprint).HasMaxLength(64).IsUnicode(false);
        b.HasIndex(x => new { x.ResourceId, x.RecordedAtUtc });
        User(b, nameof(NotificationOperationRow.ActorId));
    }

    public void Configure(EntityTypeBuilder<NotificationDeliveryAuditRow> b)
    {
        b.ToTable("TripSafety_NotificationDeliveryAudit");
        b.HasKey(x => x.AuditId);
        b.Property(x => x.AuditId).ValueGeneratedNever();
        b.Property(x => x.Outcome).HasMaxLength(48).IsUnicode(false);
        b.Property(x => x.UserId).HasMaxLength(256).IsRequired();
        b.HasOne<NotificationRecipientRow>().WithMany().HasForeignKey(x => new { x.EventId, x.UserId })
            .OnDelete(DeleteBehavior.NoAction);
    }

    private static void User<T>(EntityTypeBuilder<T> b, string property) where T : class
    {
        b.Property<string>(property).HasMaxLength(256).IsRequired();
        b.HasOne<UserAccount>().WithMany().HasForeignKey(property).OnDelete(DeleteBehavior.NoAction);
    }
}
