
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;

namespace Wayhunter.Infrastructure.Persistence.Configurations.IdentitySocial;

public sealed class IdentityConsentConfiguration
    : IEntityTypeConfiguration<IdentityConsent>
{
    public void Configure(
        EntityTypeBuilder<IdentityConsent> builder)
    {
        // Proposed schema.
        // Requires Schema Freeze and DB Coordinator review.
        builder.ToTable("IdentityConsents");

        // Primary Key
        builder.HasKey(consent => consent.ConsentId);

        builder.Property(consent => consent.ConsentId)
            .HasMaxLength(36)
            .ValueGeneratedNever()
            .IsRequired();

        // User who granted consent
        builder.Property(consent => consent.UserId)
            .HasMaxLength(256)
            .IsRequired();

        // Consent type / key
        builder.Property(consent => consent.ConsentKey)
            .HasMaxLength(200)
            .IsRequired();

        // Granted timestamp
        builder.Property(consent => consent.GrantedAtUtc)
            .HasColumnType("datetimeoffset")
            .IsRequired();

        // Optional revocation timestamp
        builder.Property(consent => consent.RevokedAtUtc)
            .HasColumnType("datetimeoffset")
            .IsRequired(false);

        // Query consent records by user and key.
        // Non-unique to preserve historical consent records.
        builder.HasIndex(consent => new
        {
            consent.UserId,
            consent.ConsentKey
        });

        // UserAccount relationship
        builder.HasOne<UserAccount>()
            .WithMany()
            .HasForeignKey(consent => consent.UserId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
