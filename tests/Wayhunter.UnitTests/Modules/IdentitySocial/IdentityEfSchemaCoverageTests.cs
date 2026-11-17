using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Xunit;

namespace Wayhunter.UnitTests.Modules.IdentitySocial;

public sealed class IdentityEfSchemaCoverageTests
{
    private static WayhunterSharedDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<WayhunterSharedDbContext>()
            .UseSqlServer(
                "Server=localhost;Database=Wayhunter_ModelOnly;" +
                "Trusted_Connection=True;TrustServerCertificate=True;")
            .Options;

        // Metadata only: no database connection.
        return new WayhunterSharedDbContext(options);
    }

    [Theory]
    [InlineData(typeof(UserAccount), "CreatedAtUtc")]
    [InlineData(typeof(UserAccount), "VerifiedAtUtc")]
    [InlineData(typeof(UserAccount), "DeletionRequestedAtUtc")]
    [InlineData(typeof(UserProfile), "CreatedAtUtc")]
    [InlineData(typeof(UserProfile), "UpdatedAtUtc")]
    [InlineData(typeof(IdentitySession), "CreatedAtUtc")]
    [InlineData(typeof(IdentityOtpChallenge), "CreatedAtUtc")]
    public void AdditionalTimestamps_UseDateTimeOffset(
        Type entityType, string propertyName)
    {
        using var db = CreateContext();
        var entity = db.Model.FindEntityType(entityType);
        Assert.NotNull(entity);

        var property = entity.FindProperty(propertyName);
        Assert.NotNull(property);
        Assert.Equal("datetimeoffset", property.GetColumnType());
    }

    [Theory]
    [InlineData(typeof(UserAccount), "UserId", true)]
    [InlineData(typeof(UserAccount), "DisplayName", true)]
    [InlineData(typeof(UserAccount), "NormalizedEmail", true)]
    [InlineData(typeof(UserAccount), "PasswordHash", true)]
    [InlineData(typeof(UserAccount), "VerifiedAtUtc", false)]
    [InlineData(typeof(UserAccount), "DeletionRequestedAtUtc", false)]
    [InlineData(typeof(UserProfile), "AvatarUrl", false)]
    [InlineData(typeof(UserProfile), "Bio", false)]
    [InlineData(typeof(UserProfile), "UpdatedAtUtc", false)]
    [InlineData(typeof(IdentitySession), "UserId", true)]
    [InlineData(typeof(IdentitySession), "RevokedAtUtc", false)]
    [InlineData(typeof(IdentityOtpChallenge), "OtpVerifierHash", true)]
    [InlineData(typeof(IdentityOtpChallenge), "ConsumedAtUtc", false)]
    [InlineData(typeof(IdentityOtpChallenge), "RevokedAtUtc", false)]
    [InlineData(typeof(IdentityConsent), "ConsentKey", true)]
    [InlineData(typeof(IdentityConsent), "RevokedAtUtc", false)]
    public void Fields_HaveExpectedNullability(
        Type entityType, string propertyName, bool required)
    {
        using var db = CreateContext();
        var entity = db.Model.FindEntityType(entityType);
        Assert.NotNull(entity);

        var property = entity.FindProperty(propertyName);
        Assert.NotNull(property);
        Assert.Equal(!required, property.IsNullable);
    }

    [Theory]
    [InlineData(typeof(IdentitySession), "UserId")]
    [InlineData(typeof(IdentitySession), "ExpiresAtUtc")]
    [InlineData(typeof(IdentityOtpChallenge), "UserId")]
    [InlineData(typeof(IdentityOtpChallenge), "ExpiresAtUtc")]
    public void SessionAndOtp_HaveQueryIndexes(
        Type entityType, string propertyName)
    {
        using var db = CreateContext();
        var entity = db.Model.FindEntityType(entityType);
        Assert.NotNull(entity);

        Assert.Contains(entity.GetIndexes(), index =>
            index.Properties.Select(p => p.Name)
                .SequenceEqual(new[] { propertyName }) &&
            !index.IsUnique);
    }

    [Fact]
    public void Consent_HasNonUniqueCompositeIndex()
    {
        using var db = CreateContext();
        var entity = db.Model.FindEntityType(typeof(IdentityConsent));
        Assert.NotNull(entity);

        Assert.Contains(entity.GetIndexes(), index =>
            index.Properties.Select(p => p.Name)
                .SequenceEqual(new[] { "UserId", "ConsentKey" }) &&
            !index.IsUnique);
    }

    [Theory]
    [InlineData(typeof(UserAccount), "DisplayName", 200)]
    [InlineData(typeof(UserAccount), "NormalizedEmail", 320)]
    [InlineData(typeof(UserAccount), "PasswordHash", 1024)]
    [InlineData(typeof(UserProfile), "AvatarUrl", 2048)]
    [InlineData(typeof(UserProfile), "Bio", 500)]
    [InlineData(typeof(IdentityOtpChallenge), "OtpVerifierHash", 1024)]
    [InlineData(typeof(IdentityConsent), "ConsentKey", 200)]
    public void AdditionalStrings_HaveExpectedLengths(
        Type entityType, string propertyName, int length)
    {
        using var db = CreateContext();
        var entity = db.Model.FindEntityType(entityType);
        Assert.NotNull(entity);

        var property = entity.FindProperty(propertyName);
        Assert.NotNull(property);
        Assert.Equal(length, property.GetMaxLength());
    }
}
