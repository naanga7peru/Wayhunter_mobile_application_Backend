using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Xunit;

namespace Wayhunter.UnitTests.Modules.IdentitySocial;

public sealed class BlockedUserRelationshipTests
{
    [Fact]
    public void Block_relationship_has_directional_composite_key_and_user_ownership()
    {
        var options = new DbContextOptionsBuilder<WayhunterSharedDbContext>()
            .UseSqlServer("Server=localhost;Database=ModelOnly;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;
        using var db = new WayhunterSharedDbContext(options);
        var entity = db.Model.FindEntityType(typeof(BlockedUserRelationship));

        Assert.NotNull(entity);
        Assert.Equal("BlockedUserRelationships", entity.GetTableName());
        Assert.Equal(new[] { "BlockerUserId", "BlockedUserId" },
            entity.FindPrimaryKey()!.Properties.Select(x => x.Name));
        Assert.Equal(2, entity.GetForeignKeys().Count(x => x.PrincipalEntityType.ClrType == typeof(UserAccount)));
        Assert.Contains(entity.GetIndexes(), x => x.GetDatabaseName() ==
            "IX_BlockedUserRelationships_BlockedUserId_BlockerUserId");
    }

    [Fact]
    public void Domain_rejects_self_block()
    {
        Assert.Throws<InvalidOperationException>(() => new BlockedUserRelationship(
            "4b6a2947-9496-48b0-bfe7-80a6117524e4",
            "4b6a2947-9496-48b0-bfe7-80a6117524e4",
            DateTimeOffset.UtcNow));
    }
}
