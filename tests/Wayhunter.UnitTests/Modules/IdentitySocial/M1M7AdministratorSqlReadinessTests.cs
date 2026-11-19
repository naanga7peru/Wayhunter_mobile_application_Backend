using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Wayhunter.Infrastructure.Persistence.Configurations.IdentitySocial;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Xunit;

namespace Wayhunter.UnitTests.Modules.IdentitySocial;

public sealed class M1M7AdministratorSqlReadinessTests
{
    [Fact]
    public void Users_UserId_MatchesRoleForeignKeyBaseline()
    {
        var builder = new ModelBuilder(new ConventionSet());

        builder.ApplyConfiguration(
            new UserAccountConfiguration());

        var model = builder.FinalizeModel();

        var users = model.FindEntityType(
            typeof(UserAccount));

        Assert.NotNull(users);
        Assert.Equal("Users", users.GetTableName());

        var primaryKey = users.FindPrimaryKey();

        Assert.NotNull(primaryKey);

        Assert.Equal(
            nameof(UserAccount.UserId),
            Assert.Single(primaryKey.Properties).Name);

        var userId = users.FindProperty(
            nameof(UserAccount.UserId));

        Assert.NotNull(userId);
        Assert.Equal(typeof(string), userId.ClrType);
        Assert.Equal(256, userId.GetMaxLength());
        Assert.False(userId.IsNullable);

        Assert.Equal(
            ValueGenerated.Never,
            userId.ValueGenerated);
    }

    [Fact]
    public void SharedModel_MapsApprovedAdminRoleTable()
    {
        var options = new DbContextOptionsBuilder<
            WayhunterSharedDbContext>()
            .UseInMemoryDatabase(
                "admin-readiness-" + Guid.NewGuid())
            .Options;

        using var context =
            new WayhunterSharedDbContext(options);

        var model = context.Model;

        var users = model.FindEntityType(
            typeof(UserAccount));

        Assert.NotNull(users);
        Assert.Equal("Users", users.GetTableName());

        // Admin SQL schema approval is still pending.
        var roleAssignments = model.GetEntityTypes()
            .SingleOrDefault(entity =>
                string.Equals(
                    entity.GetTableName(),
                    "UserRoleAssignments",
                    StringComparison.Ordinal));

        Assert.NotNull(roleAssignments);
    }
}