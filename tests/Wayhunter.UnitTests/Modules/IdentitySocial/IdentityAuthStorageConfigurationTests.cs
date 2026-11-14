
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Wayhunter.Infrastructure.Persistence.Configurations.IdentitySocial;
using Xunit;

namespace Wayhunter.UnitTests.Modules.IdentitySocial;

public sealed class IdentityAuthStorageConfigurationTests
{
    // EF metadata inspection only.
    // No database connection or migration is executed.
    private static IEntityType GetEntity<TEntity>()
        where TEntity : class
    {
        var builder = new ModelBuilder(new ConventionSet());

        builder.ApplyConfiguration(
            new UserAccountConfiguration());

        builder.ApplyConfiguration(
            new IdentitySessionConfiguration());

        builder.ApplyConfiguration(
            new IdentityOtpChallengeConfiguration());

        builder.ApplyConfiguration(
            new IdentityConsentConfiguration());

        var model = builder.FinalizeModel();

        return model.FindEntityType(typeof(TEntity))
            ?? throw new InvalidOperationException(
                "Entity was not configured.");
    }

    [Fact]
    public void Session_ShouldHavePrimaryKey()
    {
        var entity = GetEntity<IdentitySession>();

        Assert.Equal("IdentitySessions", entity.GetTableName());

        var primaryKey = entity.FindPrimaryKey();

        Assert.NotNull(primaryKey);

        Assert.Equal(
            nameof(IdentitySession.SessionId),
            Assert.Single(primaryKey.Properties).Name);
    }

    [Fact]
    public void Session_ShouldHaveUserForeignKey()
    {
        var entity = GetEntity<IdentitySession>();

        var foreignKey = Assert.Single(
            entity.GetForeignKeys());

        Assert.Equal(
            typeof(UserAccount),
            foreignKey.PrincipalEntityType.ClrType);

        Assert.Equal(
            nameof(IdentitySession.UserId),
            Assert.Single(foreignKey.Properties).Name);

        Assert.Equal(
            DeleteBehavior.NoAction,
            foreignKey.DeleteBehavior);
    }

    [Fact]
    public void Session_ShouldStoreExpiryAndRevocation()
    {
        var entity = GetEntity<IdentitySession>();

        var expiry = entity.FindProperty(
            nameof(IdentitySession.ExpiresAtUtc));

        var revoked = entity.FindProperty(
            nameof(IdentitySession.RevokedAtUtc));

        Assert.NotNull(expiry);
        Assert.NotNull(revoked);

        Assert.False(expiry.IsNullable);
        Assert.True(revoked.IsNullable);

        Assert.Equal(
            "datetimeoffset",
            expiry.FindAnnotation("Relational:ColumnType")?.Value);

        Assert.Equal(
            "datetimeoffset",
            revoked.FindAnnotation("Relational:ColumnType")?.Value);
    }

    [Fact]
    public void Session_ShouldHaveQueryIndexes()
    {
        var entity = GetEntity<IdentitySession>();

        Assert.Contains(entity.GetIndexes(),
            index => index.Properties.Any(
                property => property.Name ==
                    nameof(IdentitySession.UserId)));

        Assert.Contains(entity.GetIndexes(),
            index => index.Properties.Any(
                property => property.Name ==
                    nameof(IdentitySession.ExpiresAtUtc)));
    }

    [Fact]
    public void Otp_ShouldHavePrimaryKeyAndUserRelationship()
    {
        var entity = GetEntity<IdentityOtpChallenge>();

        Assert.Equal(
            "IdentityOtpChallenges",
            entity.GetTableName());

        Assert.Equal(
            nameof(IdentityOtpChallenge.ChallengeId),
            Assert.Single(
                entity.FindPrimaryKey()!.Properties).Name);

        var foreignKey = Assert.Single(
            entity.GetForeignKeys());

        Assert.Equal(
            typeof(UserAccount),
            foreignKey.PrincipalEntityType.ClrType);
    }

    [Fact]
    public void Otp_ShouldStoreAttemptsExpiryAndRevocation()
    {
        var entity = GetEntity<IdentityOtpChallenge>();

        Assert.NotNull(entity.FindProperty(
            nameof(IdentityOtpChallenge.OtpVerifierHash)));

        Assert.NotNull(entity.FindProperty(
            nameof(IdentityOtpChallenge.FailedAttemptCount)));

        Assert.NotNull(entity.FindProperty(
            nameof(IdentityOtpChallenge.MaxFailedAttempts)));

        var expiry = entity.FindProperty(
            nameof(IdentityOtpChallenge.ExpiresAtUtc));

        var revoked = entity.FindProperty(
            nameof(IdentityOtpChallenge.RevokedAtUtc));

        Assert.NotNull(expiry);
        Assert.NotNull(revoked);

        Assert.False(expiry.IsNullable);
        Assert.True(revoked.IsNullable);
    }

    [Fact]
    public void Consent_ShouldHaveUserRelationshipAndTimestamps()
    {
        var entity = GetEntity<IdentityConsent>();

        Assert.Equal(
            "IdentityConsents",
            entity.GetTableName());

        Assert.Equal(
            nameof(IdentityConsent.ConsentId),
            Assert.Single(
                entity.FindPrimaryKey()!.Properties).Name);

        var foreignKey = Assert.Single(
            entity.GetForeignKeys());

        Assert.Equal(
            typeof(UserAccount),
            foreignKey.PrincipalEntityType.ClrType);

        var granted = entity.FindProperty(
            nameof(IdentityConsent.GrantedAtUtc));

        var revoked = entity.FindProperty(
            nameof(IdentityConsent.RevokedAtUtc));

        Assert.NotNull(granted);
        Assert.NotNull(revoked);

        Assert.False(granted.IsNullable);
        Assert.True(revoked.IsNullable);
    }

    [Fact]
    public void Consent_ShouldHaveNonUniqueUserKeyIndex()
    {
        var entity = GetEntity<IdentityConsent>();


        var index = Assert.Single(
            entity.GetIndexes(),
            index => index.Properties
                .Select(property => property.Name)
                .SequenceEqual(new[]
                {
            nameof(IdentityConsent.UserId),
            nameof(IdentityConsent.ConsentKey)
                }));


        // Historical consent records must remain possible.
        Assert.False(index.IsUnique);
    }
}
