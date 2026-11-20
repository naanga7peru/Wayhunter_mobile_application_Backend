using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;

namespace Wayhunter.Infrastructure.Persistence.Configurations.IdentitySocial;

public sealed class IdentityPasswordResetCredentialConfiguration
    : IEntityTypeConfiguration<IdentityPasswordResetCredential>
{
    public void Configure(
        EntityTypeBuilder<IdentityPasswordResetCredential> builder)
    {
        builder.ToTable("IdentityPasswordResetCredentials", table =>
            table.HasCheckConstraint(
                "CK_IdentityPasswordResetCredentials_Expiry",
                "[ExpiresAtUtc] > [CreatedAtUtc]"));

        builder.HasKey(x => x.CredentialId)
            .HasName("PK_IdentityPasswordResetCredentials");
        builder.Property(x => x.CredentialId)
            .HasColumnType("uniqueidentifier")
            .ValueGeneratedNever()
            .IsRequired();
        builder.Property(x => x.UserId)
            .HasColumnType("nvarchar(256)")
            .HasMaxLength(256)
            .IsRequired();
        builder.Property(x => x.TokenHash)
            .HasColumnType("char(64)")
            .HasMaxLength(64)
            .IsRequired();
        builder.Property(x => x.CreatedAtUtc)
            .HasColumnType("datetimeoffset")
            .IsRequired();
        builder.Property(x => x.ExpiresAtUtc)
            .HasColumnType("datetimeoffset")
            .IsRequired();
        builder.Property(x => x.ConsumedAtUtc)
            .HasColumnType("datetimeoffset");
        builder.Property(x => x.RevokedAtUtc)
            .HasColumnType("datetimeoffset");

        builder.HasIndex(x => x.TokenHash)
            .IsUnique()
            .HasDatabaseName("UX_IdentityPasswordResetCredentials_TokenHash");
        builder.HasIndex(x => new { x.UserId, x.ExpiresAtUtc })
            .HasDatabaseName("IX_IdentityPasswordResetCredentials_User_Expiry");
        builder.HasOne<UserAccount>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_IdentityPasswordResetCredentials_Users_UserId");
    }
}
