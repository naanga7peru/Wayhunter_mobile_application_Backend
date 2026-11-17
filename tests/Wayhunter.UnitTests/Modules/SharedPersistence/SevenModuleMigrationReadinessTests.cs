using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Wayhunter.Domain.Modules.HomeAdsAnalytics.Entities;
using Wayhunter.Domain.Modules.HunterPlaces.Entities;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Wayhunter.Domain.Modules.TravelPlanning.Entities;
using Wayhunter.Domain.Modules.TripSafety.Entities;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Wayhunter.Infrastructure.Persistence.Models.CoinsWallet;
using Wayhunter.Infrastructure.Persistence.Models.Games;
using Xunit;

namespace Wayhunter.UnitTests.Modules.SharedPersistence;

public sealed class SevenModuleMigrationReadinessTests
{
    private static WayhunterSharedDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<WayhunterSharedDbContext>()
            .UseSqlServer(
                "Server=localhost;Database=Wayhunter_SevenModule_ModelOnly;" +
                "Trusted_Connection=True;TrustServerCertificate=True")
            .Options;

        return new WayhunterSharedDbContext(options);
    }

    [Theory]
    [InlineData(typeof(UserAccount), "Users")]
    [InlineData(typeof(TripPlan), "Trips")]
    [InlineData(typeof(LiveTripSession), "TripSafety_LiveTripSessions")]
    [InlineData(typeof(HunterSubmission), "Hunter_Submissions")]
    [InlineData(typeof(GameRow), "Games")]
    [InlineData(typeof(CoinWalletRow), "CoinWallets")]
    [InlineData(typeof(Advertisement), "Advertisements")]
    public void EveryModule_HasAnAuthoritativeSharedMapping(
        Type clrType,
        string tableName)
    {
        using var db = CreateContext();

        var entity = db.Model.FindEntityType(clrType);

        Assert.NotNull(entity);
        Assert.Equal(tableName, entity.GetTableName());
    }

    [Fact]
    public void SharedModel_HasNoDuplicatePhysicalTableMappings()
    {
        using var db = CreateContext();

        var duplicates = db.Model.GetEntityTypes()
            .Where(entity => entity.GetTableName() is not null)
            .GroupBy(
                entity => $"{entity.GetSchema() ?? "dbo"}.{entity.GetTableName()}",
                StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();

        Assert.Empty(duplicates);
    }

    [Fact]
    public void UserForeignKeys_MatchCanonicalStringContract()
    {
        using var db = CreateContext();

        var userEntity = db.Model.FindEntityType(typeof(UserAccount));
        Assert.NotNull(userEntity);

        var userId = userEntity.FindProperty(nameof(UserAccount.UserId));
        Assert.NotNull(userId);
        Assert.Equal(typeof(string), userId.ClrType);
        Assert.Equal(256, userId.GetMaxLength());

        var userForeignKeys = db.Model.GetEntityTypes()
            .SelectMany(entity => entity.GetForeignKeys())
            .Where(foreignKey =>
                foreignKey.PrincipalEntityType.ClrType == typeof(UserAccount))
            .ToArray();

        Assert.NotEmpty(userForeignKeys);

        foreach (var foreignKey in userForeignKeys)
        {
            Assert.Equal(DeleteBehavior.NoAction, foreignKey.DeleteBehavior);

            foreach (var property in foreignKey.Properties)
            {
                Assert.Equal(typeof(string), property.ClrType);
                Assert.Equal(256, property.GetMaxLength());
            }
        }
    }

    [Fact]
    public void DeleteBehavior_IsRetentionSafeExceptTripOwnedStops()
    {
        using var db = CreateContext();

        var nonNoAction = db.Model.GetEntityTypes()
            .SelectMany(entity => entity.GetForeignKeys())
            .Where(foreignKey => foreignKey.DeleteBehavior != DeleteBehavior.NoAction)
            .ToArray();

        var ownedTripStops = Assert.Single(nonNoAction);

        Assert.Equal(typeof(TripStop), ownedTripStops.DeclaringEntityType.ClrType);
        Assert.Equal(typeof(TripPlan), ownedTripStops.PrincipalEntityType.ClrType);
        Assert.Equal(DeleteBehavior.Cascade, ownedTripStops.DeleteBehavior);
    }

    [Fact]
    public void MutableFinancialAndGameplayAggregates_HaveConcurrencyTokens()
    {
        using var db = CreateContext();

        Type[] types =
        {
            typeof(TripPlan),
            typeof(CoinWalletRow),
            typeof(CoinRewardOutboxRow),
            typeof(GameRow),
            typeof(GameAttemptRow),
            typeof(GameCompletionResultRow)
        };

        foreach (var type in types)
        {
            var entity = db.Model.FindEntityType(type);
            Assert.NotNull(entity);
            Assert.Contains(
                entity.GetProperties(),
                property => property.IsConcurrencyToken);
        }
    }

    [Fact]
    public void SharedMigrationModel_HasNoAutomaticSeedRows()
    {
        using var db = CreateContext();

        var designModel = db.GetService<IDesignTimeModel>().Model;

        foreach (var entity in designModel.GetEntityTypes())
        {
            Assert.Empty(entity.GetSeedData());
        }
    }
}
