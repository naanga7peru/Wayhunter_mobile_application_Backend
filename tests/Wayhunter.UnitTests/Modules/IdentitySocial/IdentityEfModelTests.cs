
using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Xunit;

namespace Wayhunter.UnitTests.Modules.IdentitySocial;

public sealed class IdentityEfModelTests
{
    private static IdentityDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseSqlServer(
                "Server=localhost;Database=Wayhunter_ModelOnly;" +
                "Trusted_Connection=True;TrustServerCertificate=True;")
            .Options;

        // Only inspect EF metadata. No SQL connection is opened.
        return new IdentityDbContext(options);
    }

    [Theory]
    [InlineData(typeof(UserAccount), "Users", "UserId")]
    [InlineData(typeof(UserProfile), "UserProfiles", "UserId")]
    [InlineData(typeof(IdentitySession), "IdentitySessions", "SessionId")]
    [InlineData(typeof(IdentityOtpChallenge), "IdentityOtpChallenges", "ChallengeId")]
    [InlineData(typeof(IdentityConsent), "IdentityConsents", "ConsentId")]
    public void RequiredEntity_HasExpectedTableAndPrimaryKey(
        Type entityType,
        string tableName,
        string primaryKeyName)
    {
        using var db = CreateContext();

        var entity = db.Model.FindEntityType(entityType);

        Assert.NotNull(entity);
        Assert.Equal(tableName, entity.GetTableName());

        var key = entity.FindPrimaryKey();

        Assert.NotNull(key);
        Assert.Equal(
            new[] { primaryKeyName },
            key.Properties.Select(p => p.Name).ToArray());
    }

    [Fact]
    public void UserEmail_HasUniqueIndex()
    {
        using var db = CreateContext();

        var entity = db.Model.FindEntityType(typeof(UserAccount));
        Assert.NotNull(entity);

        var index = entity.GetIndexes().SingleOrDefault(i =>
            i.Properties.Select(p => p.Name)
                .SequenceEqual(new[] { "NormalizedEmail" }));

        Assert.NotNull(index);
        Assert.True(index.IsUnique);
        Assert.Equal(
            "UX_Users_NormalizedEmail",
            index.GetDatabaseName());
    }

    [Theory]
    [InlineData(typeof(UserProfile))]
    [InlineData(typeof(IdentitySession))]
    [InlineData(typeof(IdentityOtpChallenge))]
    [InlineData(typeof(IdentityConsent))]
    public void UserOwnedEntity_HasUserForeignKey(Type entityType)
    {
        using var db = CreateContext();

        var entity = db.Model.FindEntityType(entityType);
        Assert.NotNull(entity);

        var fk = entity.GetForeignKeys().SingleOrDefault(f =>
            f.PrincipalEntityType.ClrType == typeof(UserAccount) &&
            f.Properties.Select(p => p.Name)
                .SequenceEqual(new[] { "UserId" }));

        Assert.NotNull(fk);
        Assert.Equal(DeleteBehavior.NoAction, fk.DeleteBehavior);
    }

    [Theory]
    [InlineData(typeof(IdentitySession), "ExpiresAtUtc")]
    [InlineData(typeof(IdentitySession), "RevokedAtUtc")]
    [InlineData(typeof(IdentityOtpChallenge), "ExpiresAtUtc")]
    [InlineData(typeof(IdentityOtpChallenge), "ConsumedAtUtc")]
    [InlineData(typeof(IdentityOtpChallenge), "RevokedAtUtc")]
    [InlineData(typeof(IdentityConsent), "GrantedAtUtc")]
    [InlineData(typeof(IdentityConsent), "RevokedAtUtc")]
    public void TimeFields_UseSqlServerDateTimeOffset(
        Type entityType,
        string propertyName)
    {
        using var db = CreateContext();

        var entity = db.Model.FindEntityType(entityType);
        Assert.NotNull(entity);

        var property = entity.FindProperty(propertyName);
        Assert.NotNull(property);

        Assert.Equal("datetimeoffset", property.GetColumnType());
    }

    [Fact]
    public void UserProfile_UsesUserIdAsSharedPrimaryKey()
    {
        using var db = CreateContext();

        var entity = db.Model.FindEntityType(typeof(UserProfile));
        Assert.NotNull(entity);

        var key = entity.FindPrimaryKey();
        Assert.NotNull(key);

        Assert.Equal("UserId", Assert.Single(key.Properties).Name);

        var fk = Assert.Single(entity.GetForeignKeys());
        Assert.True(fk.IsUnique);
    }

    [Fact]
    public void IdentityIds_HaveExpectedMaxLengths()
    {
        using var db = CreateContext();

        AssertMaxLength(db, typeof(UserAccount), "UserId", 256);
        AssertMaxLength(db, typeof(UserProfile), "UserId", 256);
        AssertMaxLength(db, typeof(IdentitySession), "UserId", 256);
        AssertMaxLength(db, typeof(IdentitySession), "SessionId", 36);
        AssertMaxLength(db, typeof(IdentityOtpChallenge), "UserId", 256);
        AssertMaxLength(db, typeof(IdentityOtpChallenge), "ChallengeId", 36);
        AssertMaxLength(db, typeof(IdentityConsent), "UserId", 256);
        AssertMaxLength(db, typeof(IdentityConsent), "ConsentId", 36);
    }

    private static void AssertMaxLength(
        IdentityDbContext db,
        Type entityType,
        string propertyName,
        int expectedLength)
    {
        var entity = db.Model.FindEntityType(entityType);
        Assert.NotNull(entity);

        var property = entity.FindProperty(propertyName);
        Assert.NotNull(property);

        Assert.Equal(expectedLength, property.GetMaxLength());
    }
}
