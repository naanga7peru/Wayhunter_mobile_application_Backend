
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;

namespace Wayhunter.Infrastructure.Persistence.Configurations.IdentitySocial;

public sealed class UserProfileConfiguration
    : IEntityTypeConfiguration<UserProfile>
{
    public void Configure(EntityTypeBuilder<UserProfile> builder)
    {
        // Proposed table
        builder.ToTable("UserProfiles");

        // UserId is both Primary Key and Foreign Key
        builder.HasKey(profile => profile.UserId);

        builder.Property(profile => profile.UserId)
            .HasMaxLength(256)
            .ValueGeneratedNever()
            .IsRequired();

        builder.Property(profile => profile.AvatarUrl)
            .HasMaxLength(2048)
            .IsRequired(false);

        builder.Property(profile => profile.Bio)
            .HasMaxLength(500)
            .IsRequired(false);

        builder.Property(profile => profile.CreatedAtUtc)
            .HasColumnType("datetimeoffset")
            .IsRequired();

        builder.Property(profile => profile.UpdatedAtUtc)
            .HasColumnType("datetimeoffset")
            .IsRequired(false);

        // One UserAccount has at most one UserProfile.
        builder.HasOne<UserAccount>()
            .WithOne()
            .HasForeignKey<UserProfile>(
                profile => profile.UserId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
