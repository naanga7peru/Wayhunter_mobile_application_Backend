
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Wayhunter.Domain.Modules.TripSafety.Entities;

namespace Wayhunter.Infrastructure.Persistence.Configurations.TripSafety;

public sealed class TripGpsConsentConfiguration
    : IEntityTypeConfiguration<TripGpsConsent>
{
    public void Configure(
        EntityTypeBuilder<TripGpsConsent> builder)
    {
        builder.ToTable(
            "TripSafety_GpsConsents",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_TripSafety_GpsConsents_GrantState",
                    "([IsGranted] = 0) OR " +
                    "([IsGranted] = 1 AND " +
                    "[GrantedAtUtc] IS NOT NULL AND " +
                    "[RevokedAtUtc] IS NULL)");

                table.HasCheckConstraint(
                    "CK_TripSafety_GpsConsents_RevocationTime",
                    "[RevokedAtUtc] IS NULL OR " +
                    "([GrantedAtUtc] IS NOT NULL AND " +
                    "[RevokedAtUtc] >= [GrantedAtUtc])");
            });

        // Composite primary key:
        // One consent record per session/user.
        builder.HasKey(x => new
        {
            x.SessionId,
            x.UserId
        });

        builder.Property(x => x.SessionId)
            .HasColumnType("uniqueidentifier")
            .IsRequired();

        // M1 User ID
        builder.Property(x => x.UserId)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(x => x.IsGranted)
            .IsRequired();

        builder.Property(x => x.GrantedAtUtc)
            .HasColumnType("datetimeoffset")
            .IsRequired(false);

        builder.Property(x => x.RevokedAtUtc)
            .HasColumnType("datetimeoffset")
            .IsRequired(false);

        // M3 Live Trip Session FK
        builder.HasOne<LiveTripSession>()
            .WithMany()
            .HasForeignKey(x => x.SessionId)
            .HasPrincipalKey(x => x.SessionId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName(
                "FK_TripSafety_GpsConsents_LiveTripSessions_SessionId");

        // M1 User FK
        builder.HasOne<UserAccount>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .HasPrincipalKey(x => x.UserId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName(
                "FK_TripSafety_GpsConsents_Users_UserId");

        // User-side consent lookup.
        builder.HasIndex(x => x.UserId)
            .HasDatabaseName(
                "IX_TripSafety_GpsConsents_UserId");
    }
}
