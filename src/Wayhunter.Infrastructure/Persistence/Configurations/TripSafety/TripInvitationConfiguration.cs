
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Wayhunter.Domain.Modules.TravelPlanning.Entities;
using Wayhunter.Domain.Modules.TripSafety.Entities;

namespace Wayhunter.Infrastructure.Persistence.Configurations.TripSafety;

public sealed class TripInvitationConfiguration
    : IEntityTypeConfiguration<TripInvitation>
{
    public void Configure(
        EntityTypeBuilder<TripInvitation> builder)
    {
        builder.ToTable(
            "TripSafety_TripInvitations",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_TripSafety_TripInvitations_Status",
                    "[Status] IN (0, 1, 2, 3)");

                table.HasCheckConstraint(
                    "CK_TripSafety_TripInvitations_ResponseTime",
                    "([Status] = 0 AND [RespondedAtUtc] IS NULL) OR " +
                    "([Status] IN (1, 2, 3) AND " +
                    "[RespondedAtUtc] IS NOT NULL AND " +
                    "[RespondedAtUtc] >= [CreatedAtUtc])");
            });

        // Primary key
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        // M2 TripPlan contract: Guid
        builder.Property(x => x.TripPlanId)
            .HasColumnType("uniqueidentifier")
            .IsRequired();

        // M1 UserAccount.UserId contract: string, max 256
        builder.Property(x => x.SenderUserId)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(x => x.RecipientUserId)
            .HasMaxLength(256)
            .IsRequired();

        // Invitation lifecycle
        builder.Property(x => x.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnType("datetimeoffset")
            .IsRequired();

        builder.Property(x => x.RespondedAtUtc)
            .HasColumnType("datetimeoffset")
            .IsRequired(false);

        // M2 Trip relationship
        builder.HasOne<TripPlan>()
            .WithMany()
            .HasForeignKey(x => x.TripPlanId)
            .HasPrincipalKey(x => x.Id)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName(
                "FK_TripSafety_TripInvitations_Trips_TripPlanId");

        // M1 Sender relationship
        builder.HasOne<UserAccount>()
            .WithMany()
            .HasForeignKey(x => x.SenderUserId)
            .HasPrincipalKey(x => x.UserId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName(
                "FK_TripSafety_TripInvitations_Users_SenderUserId");

        // M1 Recipient relationship
        builder.HasOne<UserAccount>()
            .WithMany()
            .HasForeignKey(x => x.RecipientUserId)
            .HasPrincipalKey(x => x.UserId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName(
                "FK_TripSafety_TripInvitations_Users_RecipientUserId");

        // One open or accepted invitation per
        // trip and recipient.
        //
        // Pending = 0
        // Accepted = 1

        builder.HasIndex(x => new
        {
            x.TripPlanId,
            x.RecipientUserId
        })
        .IsUnique()
        .HasFilter("[Status] IN (0, 1)")
        .HasDatabaseName(
            "UX_TripSafety_TripInvitations_OpenRecipient");

        // Sender-side invitation lookup.
        builder.HasIndex(x => new
        {
            x.SenderUserId,
            x.CreatedAtUtc
        })
        .HasDatabaseName(
            "IX_TripSafety_TripInvitations_Sender_Created");

        // Recipient-side invitation lookup.
        builder.HasIndex(x => new
        {
            x.RecipientUserId,
            x.Status
        })
        .HasDatabaseName(
            "IX_TripSafety_TripInvitations_Recipient_Status");
    }
}
