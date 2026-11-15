
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Wayhunter.Infrastructure.Persistence.Configurations.IdentitySocial;
using Xunit;

namespace Wayhunter.UnitTests.Modules.IdentitySocial;

public sealed class IdentityMigrationReadinessTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 11, 15, 0, 0, 0, TimeSpan.Zero);

    private static UserAccount CreateUser(
        string name,
        string email)
    {
        return UserAccount.RegisterPending(
            name,
            email,
            "test-only-placeholder-hash",
            Now);
    }

    [Fact]
    public void Users_ShouldHaveStableUserIdPrimaryKey()
    {
        var builder = new ModelBuilder(new ConventionSet());

        builder.ApplyConfiguration(
            new UserAccountConfiguration());

        var model = builder.FinalizeModel();

        var entity = model.FindEntityType(
            typeof(UserAccount));

        Assert.NotNull(entity);
        Assert.Equal("Users", entity.GetTableName());

        var key = entity.FindPrimaryKey();

        Assert.NotNull(key);
        Assert.Equal(
            nameof(UserAccount.UserId),
            Assert.Single(key.Properties).Name);
    }

    [Fact]
    public void Users_ShouldConfigureUniqueNormalizedEmail()
    {
        var builder = new ModelBuilder(new ConventionSet());

        builder.ApplyConfiguration(
            new UserAccountConfiguration());

        var model = builder.FinalizeModel();

        var entity = model.FindEntityType(
            typeof(UserAccount));

        Assert.NotNull(entity);

        var index = Assert.Single(
            entity.GetIndexes(),
            item => item.Properties.Count == 1 &&
                item.Properties[0].Name ==
                    nameof(UserAccount.NormalizedEmail));

        Assert.True(index.IsUnique);
    }

    [Fact]
    public void EmailCaseVariants_ShouldNormalizeEqually()
    {
        var first = CreateUser(
            "First Traveler",
            " USER@EXAMPLE.COM ");

        var second = CreateUser(
            "Second Traveler",
            "user@example.com");

        Assert.Equal(
            first.NormalizedEmail,
            second.NormalizedEmail);

        Assert.NotEqual(
            first.UserId,
            second.UserId);
    }

    [Fact]
    public void DisplayNameEdit_ShouldPreserveProfileUserId()
    {
        var user = CreateUser(
            "Original Traveler",
            "traveler@example.com");

        var profile = UserProfile.CreateForUser(
            user,
            Now);

        var originalUserId = user.UserId;

        user.UpdateDisplayName("Updated Traveler");

        Assert.Equal(originalUserId, user.UserId);
        Assert.Equal(originalUserId, profile.UserId);
        Assert.Equal(
            "Updated Traveler",
            user.DisplayName);
    }

    [Fact]
    public void Profile_ShouldUseSameUserIdForeignKey()
    {
        var builder = new ModelBuilder(new ConventionSet());

        builder.ApplyConfiguration(
            new UserAccountConfiguration());

        builder.ApplyConfiguration(
            new UserProfileConfiguration());

        var model = builder.FinalizeModel();

        var profile = model.FindEntityType(
            typeof(UserProfile));

        Assert.NotNull(profile);

        var foreignKey = Assert.Single(
            profile.GetForeignKeys());

        Assert.True(foreignKey.IsUnique);

        Assert.Equal(
            nameof(UserProfile.UserId),
            Assert.Single(foreignKey.Properties).Name);

        Assert.Equal(
            typeof(UserAccount),
            foreignKey.PrincipalEntityType.ClrType);
    }
}
