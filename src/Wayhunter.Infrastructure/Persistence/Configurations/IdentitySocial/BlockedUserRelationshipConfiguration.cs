using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;

namespace Wayhunter.Infrastructure.Persistence.Configurations.IdentitySocial;

public sealed class BlockedUserRelationshipConfiguration
    : IEntityTypeConfiguration<BlockedUserRelationship>
{
    public void Configure(EntityTypeBuilder<BlockedUserRelationship> builder)
    {
        builder.ToTable("BlockedUserRelationships", table =>
            table.HasCheckConstraint(
                "CK_BlockedUserRelationships_NoSelfBlock",
                "[BlockerUserId] <> [BlockedUserId]"));

        builder.HasKey(x => new { x.BlockerUserId, x.BlockedUserId });
        builder.Property(x => x.BlockerUserId).HasMaxLength(256).IsRequired();
        builder.Property(x => x.BlockedUserId).HasMaxLength(256).IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasColumnType("datetimeoffset").IsRequired();

        builder.HasOne<UserAccount>().WithMany()
            .HasForeignKey(x => x.BlockerUserId).HasPrincipalKey(x => x.UserId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_BlockedUserRelationships_Users_BlockerUserId");
        builder.HasOne<UserAccount>().WithMany()
            .HasForeignKey(x => x.BlockedUserId).HasPrincipalKey(x => x.UserId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_BlockedUserRelationships_Users_BlockedUserId");

        builder.HasIndex(x => new { x.BlockedUserId, x.BlockerUserId })
            .HasDatabaseName("IX_BlockedUserRelationships_BlockedUserId_BlockerUserId");
    }
}
