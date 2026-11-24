using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Wayhunter.Infrastructure.Persistence.Models.Games;

namespace Wayhunter.Infrastructure.Persistence.Configurations.Games;

public sealed class GameDraftCreateOperationRowConfiguration
    : IEntityTypeConfiguration<GameDraftCreateOperationRow>
{
    public void Configure(EntityTypeBuilder<GameDraftCreateOperationRow> b)
    {
        b.ToTable("GameDraftCreateOperations");
        b.HasKey(x => new { x.CreatorUserId, x.IdempotencyKey });
        b.Property(x => x.CreatorUserId).HasMaxLength(256).IsRequired();
        b.Property(x => x.IdempotencyKey).HasMaxLength(128).IsRequired();
        b.Property(x => x.PayloadHash).HasColumnType("binary(32)").IsRequired();
        b.Property(x => x.CreatedAtUtc).HasColumnType("datetimeoffset").IsRequired();
        b.HasOne<GameRow>().WithMany().HasForeignKey(x => x.GameId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.CreatorUserId).OnDelete(DeleteBehavior.NoAction);
        b.HasIndex(x => x.GameId).IsUnique().HasFilter("[GameId] IS NOT NULL")
            .HasDatabaseName("UX_GameDraftCreateOperations_Game");
        b.HasIndex(x => x.CreatedAtUtc).HasDatabaseName("IX_GameDraftCreateOperations_CreatedAt");
    }
}
