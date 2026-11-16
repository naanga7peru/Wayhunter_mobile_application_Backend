
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Wayhunter.Domain.Modules.TravelPlanning.Entities;
using Wayhunter.Domain.Modules.TripSafety.Entities;

namespace Wayhunter.Infrastructure.Persistence.Configurations.TripSafety;

public sealed class TripMemberConfiguration
    : IEntityTypeConfiguration<TripMember>
{
    public void Configure(
        EntityTypeBuilder<TripMember> builder)
    {
        builder.ToTable(
            "TripSafety_TripMembers",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_TripSafety_TripMembers_RemovalState",
                    "([IsActive] = 1 AND [RemovedAtUtc] IS NULL) OR " +
                    "([IsActive] = 0 AND [RemovedAtUtc] IS NOT NULL AND " +
                    "[RemovedAtUtc] >= [JoinedAtUtc])");
            });

        // Primary key
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        // M2 TripPlan ID
        builder.Property(x => x.TripPlanId)
            .HasColumnType("uniqueidentifier")
            .IsRequired();

        // M1 User ID
        builder.Property(x => x.MemberUserId)
            .HasMaxLength(256)
            .IsRequired();

        // Invitation relationship
        builder.Property(x => x.InvitationId)
            .HasColumnType("uniqueidentifier")
            .IsRequired();

        // Membership timestamps
        builder.Property(x => x.JoinedAtUtc)
            .HasColumnType("datetimeoffset")
            .IsRequired();

        builder.Property(x => x.IsActive)
            .IsRequired();

        builder.Property(x => x.RemovedAtUtc)
            .HasColumnType("datetimeoffset")
            .IsRequired(false);

        // M2 Trip FK
        builder.HasOne<TripPlan>()
            .WithMany()
            .HasForeignKey(x => x.TripPlanId)
            .HasPrincipalKey(x => x.Id)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName(
                "FK_TripSafety_TripMembers_Trips_TripPlanId");

        // M1 User FK
        builder.HasOne<UserAccount>()
            .WithMany()
            .HasForeignKey(x => x.MemberUserId)
            .HasPrincipalKey(x => x.UserId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName(
                "FK_TripSafety_TripMembers_Users_MemberUserId");

        // One accepted invitation creates
        // at most one membership.
        builder.HasOne<TripInvitation>()
            .WithMany()
            .HasForeignKey(x => x.InvitationId)
            .HasPrincipalKey(x => x.Id)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName(
                "FK_TripSafety_TripMembers_TripInvitations_InvitationId");

        builder.HasIndex(x => x.InvitationId)
            .IsUnique()
            .HasDatabaseName(
                "UX_TripSafety_TripMembers_InvitationId");

        // Historical inactive members remain stored.
        // Only one active membership per trip/user.

        builder.HasIndex(x => new
        {
            x.TripPlanId,
            x.MemberUserId
        })
        .IsUnique()
        .HasFilter("[IsActive] = 1")
        .HasDatabaseName(
            "UX_TripSafety_TripMembers_ActiveTripUser");

        builder.HasIndex(x => new
        {
            x.TripPlanId,
            x.IsActive
        })
        .HasDatabaseName(
            "IX_TripSafety_TripMembers_Trip_Active");
    }
}
