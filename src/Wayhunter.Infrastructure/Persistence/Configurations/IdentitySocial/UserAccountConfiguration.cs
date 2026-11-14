
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;

namespace Wayhunter.Infrastructure.Persistence.Configurations.IdentitySocial;

public sealed class UserAccountConfiguration
    : IEntityTypeConfiguration<UserAccount>
{
    public void Configure(EntityTypeBuilder<UserAccount> builder)
    {
        // Proposed table name - requires Schema Freeze approval
        builder.ToTable("Users");

        // Stable server-generated UserId
        builder.HasKey(user => user.UserId);

        builder.Property(user => user.UserId)
            .HasMaxLength(256)
            .ValueGeneratedNever()
            .IsRequired();

        builder.Property(user => user.DisplayName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(user => user.NormalizedEmail)
            .HasMaxLength(320)
            .IsRequired();

        // Store a secure password hash, never a plain password
        builder.Property(user => user.PasswordHash)
            .HasMaxLength(1024)
            .IsRequired();

        builder.Property(user => user.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(user => user.CreatedAtUtc)
            .HasColumnType("datetimeoffset")
            .IsRequired();

        builder.Property(user => user.VerifiedAtUtc)
            .HasColumnType("datetimeoffset")
            .IsRequired(false);

        builder.Property(user => user.DeletionRequestedAtUtc)
            .HasColumnType("datetimeoffset")
            .IsRequired(false);


        // M1-MS02-T02: Proposed unique email constraint.
        // Requires Schema Freeze and DB Coordinator review.
        builder.HasIndex(user => user.NormalizedEmail)
            .IsUnique()
            .HasDatabaseName("UX_Users_NormalizedEmail");

    }
}
