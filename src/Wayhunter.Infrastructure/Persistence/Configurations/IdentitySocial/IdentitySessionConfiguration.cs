
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;

namespace Wayhunter.Infrastructure.Persistence.Configurations.IdentitySocial;

public sealed class IdentitySessionConfiguration
    : IEntityTypeConfiguration<IdentitySession>
{
    public void Configure(
        EntityTypeBuilder<IdentitySession> builder)
    {
        // Proposed schema. Requires DB Coordinator review.
        builder.ToTable("IdentitySessions");

        builder.HasKey(session => session.SessionId);

        builder.Property(session => session.SessionId)
            .HasMaxLength(36)
            .ValueGeneratedNever()
            .IsRequired();

        builder.Property(session => session.UserId)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(session => session.CreatedAtUtc)
            .HasColumnType("datetimeoffset")
            .IsRequired();

        builder.Property(session => session.ExpiresAtUtc)
            .HasColumnType("datetimeoffset")
            .IsRequired();

        builder.Property(session => session.RevokedAtUtc)
            .HasColumnType("datetimeoffset")
            .IsRequired(false);

        builder.HasIndex(session => session.UserId);

        builder.HasIndex(session => session.ExpiresAtUtc);

        builder.HasOne<UserAccount>()
            .WithMany()
            .HasForeignKey(session => session.UserId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
