
using Microsoft.EntityFrameworkCore;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Xunit;

namespace Wayhunter.UnitTests.Modules.IdentitySocial;

public sealed class WayhunterSharedDbContextModelTests
{
    private static WayhunterSharedDbContext CreateContext()
    {
        // Model inspection only. No SQL connection opened.
        var options =
            new DbContextOptionsBuilder<WayhunterSharedDbContext>()
                .UseSqlServer(
                    "Server=localhost;" +
                    "Database=WayhunterModelInspection;" +
                    "Trusted_Connection=True;" +
                    "TrustServerCertificate=True")
                .Options;

        return new WayhunterSharedDbContext(options);
    }

    [Fact]
    public void UserAccount_MapsToUsersTable()
    {
        using var db = CreateContext();

        var entity = db.Model.FindEntityType(
            typeof(UserAccount));

        Assert.NotNull(entity);
        Assert.Equal("Users", entity.GetTableName());
    }

    [Fact]
    public void IdentitySession_MapsToIdentitySessionsTable()
    {
        using var db = CreateContext();

        var entity = db.Model.FindEntityType(
            typeof(IdentitySession));

        Assert.NotNull(entity);

        Assert.Equal(
            "IdentitySessions",
            entity.GetTableName());
    }

    [Fact]
    public void Users_HaveUniqueNormalizedEmailIndex()
    {
        using var db = CreateContext();

        var entity = db.Model.FindEntityType(
            typeof(UserAccount));

        Assert.NotNull(entity);

        var index = Assert.Single(
            entity.GetIndexes(),
            index => index.Properties.Any(
                property =>
                    property.Name ==
                    nameof(UserAccount.NormalizedEmail)));

        Assert.True(index.IsUnique);
    }

    [Fact]
    public void IdentitySession_HasUserIdForeignKey()
    {
        using var db = CreateContext();

        var entity = db.Model.FindEntityType(
            typeof(IdentitySession));

        Assert.NotNull(entity);

        var foreignKey = Assert.Single(
            entity.GetForeignKeys(),
            fk => fk.Properties.Any(
                property =>
                    property.Name ==
                    nameof(IdentitySession.UserId)));

        Assert.Equal(
            typeof(UserAccount),
            foreignKey.PrincipalEntityType.ClrType);
    }

    [Fact]
    public void IdentitySession_HasRevocationTimestamp()
    {
        using var db = CreateContext();

        var entity = db.Model.FindEntityType(
            typeof(IdentitySession));

        Assert.NotNull(entity);

        var property = entity.FindProperty(
            nameof(IdentitySession.RevokedAtUtc));

        Assert.NotNull(property);
        Assert.True(property.IsNullable);
    }
}
