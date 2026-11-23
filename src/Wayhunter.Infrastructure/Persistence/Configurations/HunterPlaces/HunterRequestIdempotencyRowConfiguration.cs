using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Wayhunter.Infrastructure.Persistence.Models.HunterPlaces;

namespace Wayhunter.Infrastructure.Persistence.Configurations.HunterPlaces;

public sealed class HunterRequestIdempotencyRowConfiguration : IEntityTypeConfiguration<HunterRequestIdempotencyRow>
{
    public void Configure(EntityTypeBuilder<HunterRequestIdempotencyRow> builder)
    {
        builder.ToTable("Hunter_RequestIdempotency", table => table.HasCheckConstraint(
            "CK_Hunter_RequestIdempotency_DigestLengths", "LEN([KeyHash]) = 64 AND LEN([RequestDigest]) = 64"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.OwnerUserId).HasMaxLength(256).IsRequired();
        builder.Property(x => x.Operation).HasMaxLength(40).IsRequired();
        builder.Property(x => x.KeyHash).HasMaxLength(64).IsRequired();
        builder.Property(x => x.RequestDigest).HasMaxLength(64).IsRequired();
        builder.Property(x => x.ResourceId).IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasColumnType("datetimeoffset").IsRequired();
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.OwnerUserId)
            .OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_Hunter_RequestIdempotency_Users");
        builder.HasIndex(x => new { x.OwnerUserId, x.Operation, x.KeyHash }).IsUnique()
            .HasDatabaseName("UX_Hunter_RequestIdempotency_Owner_Operation_Key");
    }
}
