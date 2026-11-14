
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Wayhunter.Infrastructure.Persistence.Configurations.IdentitySocial;
using Xunit;

namespace Wayhunter.UnitTests.Modules.IdentitySocial;

public sealed class IdentitySchemaConfigurationTests
{
    // Build EF metadata in memory.
    // No SQL Server connection or migration is used.
    private static IModel BuildModel()
    {
        var builder = new ModelBuilder(new ConventionSet());

        builder.ApplyConfiguration(
            new UserAccountConfiguration());

        builder.ApplyConfiguration(
            new UserProfileConfiguration());

        return builder.FinalizeModel();
    }

    [Fact]
    public void UserAccount_ShouldMapToUsersTable()
    {
        var model = BuildModel();

        var entity = model.FindEntityType(
            typeof(UserAccount));

        Assert.NotNull(entity);
        Assert.Equal("Users", entity.GetTableName());
    }

    [Fact]
    public void UserProfile_ShouldMapToUserProfilesTable()
    {
        var model = BuildModel();

        var entity = model.FindEntityType(
            typeof(UserProfile));

        Assert.NotNull(entity);
        Assert.Equal(
            "UserProfiles",
            entity.GetTableName());
    }

    [Fact]
    public void UserAccount_ShouldUseUserIdAsPrimaryKey()
    {
        var model = BuildModel();
        var entity = model.FindEntityType(
            typeof(UserAccount))!;

        var primaryKey = entity.FindPrimaryKey();

        Assert.NotNull(primaryKey);

        var keyProperty =
            Assert.Single(primaryKey.Properties);

        Assert.Equal("UserId", keyProperty.Name);
        Assert.Equal(
            ValueGenerated.Never,
            keyProperty.ValueGenerated);
    }

    [Fact]
    public void UserProfile_ShouldUseUserIdAsPrimaryKey()
    {
        var model = BuildModel();
        var entity = model.FindEntityType(
            typeof(UserProfile))!;

        var primaryKey = entity.FindPrimaryKey();

        Assert.NotNull(primaryKey);

        Assert.Equal(
            "UserId",
            Assert.Single(primaryKey.Properties).Name);
    }

    [Fact]
    public void Profile_ShouldHaveOneToOneUserRelationship()
    {
        var model = BuildModel();
        var profile = model.FindEntityType(
            typeof(UserProfile))!;

        var foreignKey =
            Assert.Single(profile.GetForeignKeys());

        Assert.Equal(
            typeof(UserAccount),
            foreignKey.PrincipalEntityType.ClrType);

        Assert.Equal(
            "UserId",
            Assert.Single(foreignKey.Properties).Name);

        Assert.True(foreignKey.IsUnique);
    }

    [Fact]
    public void Profile_ShouldNotCascadeDeleteUserData()
    {
        var model = BuildModel();
        var profile = model.FindEntityType(
            typeof(UserProfile))!;

        var foreignKey =
            Assert.Single(profile.GetForeignKeys());

        Assert.Equal(
            DeleteBehavior.NoAction,
            foreignKey.DeleteBehavior);
    }

    [Fact]
    public void UserEmail_ShouldBeRequired()
    {
        var model = BuildModel();
        var user = model.FindEntityType(
            typeof(UserAccount))!;

        var email = user.FindProperty(
            nameof(UserAccount.NormalizedEmail));

        Assert.NotNull(email);
        Assert.False(email.IsNullable);
        Assert.Equal(320, email.GetMaxLength());
    }

    [Fact]
    public void ProfilePublicFields_ShouldBeOptional()
    {
        var model = BuildModel();
        var profile = model.FindEntityType(
            typeof(UserProfile))!;

        var avatar = profile.FindProperty(
            nameof(UserProfile.AvatarUrl));

        var bio = profile.FindProperty(
            nameof(UserProfile.Bio));

        Assert.NotNull(avatar);
        Assert.NotNull(bio);

        Assert.True(avatar.IsNullable);
        Assert.True(bio.IsNullable);

        Assert.Equal(2048, avatar.GetMaxLength());
        Assert.Equal(500, bio.GetMaxLength());
    }

    [Fact]
    public void UserTimestamps_ShouldUseDateTimeOffset()
    {
        var model = BuildModel();
        var user = model.FindEntityType(
            typeof(UserAccount))!;

        var created = user.FindProperty(
            nameof(UserAccount.CreatedAtUtc));

        var verified = user.FindProperty(
            nameof(UserAccount.VerifiedAtUtc));

        Assert.NotNull(created);
        Assert.NotNull(verified);


        Assert.Equal(
            "datetimeoffset",
            created.FindAnnotation("Relational:ColumnType")?.Value);

        Assert.Equal(
            "datetimeoffset",
            verified.FindAnnotation("Relational:ColumnType")?.Value);


        Assert.False(created.IsNullable);
        Assert.True(verified.IsNullable);
    }

    [Fact]
    public void ProfileTimestamps_ShouldUseDateTimeOffset()
    {
        var model = BuildModel();
        var profile = model.FindEntityType(
            typeof(UserProfile))!;

        var created = profile.FindProperty(
            nameof(UserProfile.CreatedAtUtc));

        var updated = profile.FindProperty(
            nameof(UserProfile.UpdatedAtUtc));

        Assert.NotNull(created);
        Assert.NotNull(updated);

        Assert.Equal(
            "datetimeoffset",
            created.FindAnnotation("Relational:ColumnType")?.Value);

        Assert.Equal(
            "datetimeoffset",
            updated.FindAnnotation("Relational:ColumnType")?.Value);

        Assert.False(created.IsNullable);
        Assert.True(updated.IsNullable);
    }
}
