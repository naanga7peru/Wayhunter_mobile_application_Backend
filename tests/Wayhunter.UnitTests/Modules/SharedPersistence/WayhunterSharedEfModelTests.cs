
using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Wayhunter.Domain.Modules.TravelPlanning.Entities;
using Wayhunter.Domain.Modules.TripSafety.Entities;
using Wayhunter.Domain.Modules.HomeAdsAnalytics.Entities;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Xunit;

namespace Wayhunter.UnitTests.Modules.SharedPersistence;

public sealed class WayhunterSharedEfModelTests
{
    private static WayhunterSharedDbContext CreateContext()
    {
        // EF model metadata only.
        // No database connection is opened.
        var options =
            new DbContextOptionsBuilder<WayhunterSharedDbContext>()
                .UseSqlServer(
                    "Server=localhost;" +
                    "Database=Wayhunter_ModelOnly;" +
                    "Trusted_Connection=True;" +
                    "TrustServerCertificate=True")
                .Options;

        return new WayhunterSharedDbContext(options);
    }

    [Fact]
    public void SharedModel_ContainsExistingMappedEntities()
    {
        using var context = CreateContext();

        var model = context.Model;

        Assert.NotNull(
            model.FindEntityType(typeof(UserAccount)));

        Assert.NotNull(
            model.FindEntityType(typeof(UserProfile)));

        Assert.NotNull(
            model.FindEntityType(typeof(IdentityOtpChallenge)));

        Assert.NotNull(
            model.FindEntityType(typeof(IdentitySession)));

        Assert.NotNull(
            model.FindEntityType(typeof(IdentityConsent)));

        Assert.NotNull(
            model.FindEntityType(typeof(TripPlan)));

        Assert.NotNull(
            model.FindEntityType(typeof(TripStop)));

        Assert.NotNull(
            model.FindEntityType(typeof(LiveTripSession)));

        Assert.NotNull(
            model.FindEntityType(typeof(Advertisement)));
    }

    [Fact]
    public void M1_UserId_IsStringPrimaryKey()
    {
        using var context = CreateContext();

        var entity = context.Model.FindEntityType(
            typeof(UserAccount));

        Assert.NotNull(entity);
        Assert.Equal("Users", entity.GetTableName());

        var key = entity.FindPrimaryKey();

        Assert.NotNull(key);

        Assert.Equal(
            nameof(UserAccount.UserId),
            Assert.Single(key.Properties).Name);

        var userId = entity.FindProperty(
            nameof(UserAccount.UserId));

        Assert.NotNull(userId);
        Assert.Equal(256, userId.GetMaxLength());
    }

    [Fact]
    public void M2_TripStop_HasTripForeignKey()
    {
        using var context = CreateContext();

        var stop = context.Model.FindEntityType(
            typeof(TripStop));

        Assert.NotNull(stop);

        Assert.Contains(
            stop.GetForeignKeys(),
            fk =>
                fk.PrincipalEntityType.ClrType ==
                    typeof(TripPlan) &&
                fk.Properties.Any(p =>
                    p.Name == nameof(TripStop.TripPlanId)));
    }

    [Fact]
    public void M3_LiveTripSession_HasM2TripForeignKey()
    {
        using var context = CreateContext();

        var session = context.Model.FindEntityType(
            typeof(LiveTripSession));

        Assert.NotNull(session);

        Assert.Equal(
            "TripSafety_LiveTripSessions",
            session.GetTableName());

        Assert.Contains(
            session.GetForeignKeys(),
            fk =>
                fk.PrincipalEntityType.ClrType ==
                    typeof(TripPlan) &&
                fk.Properties.Any(p =>
                    p.Name == nameof(
                        LiveTripSession.TripPlanId)));
    }

    [Fact]
    public void M7_Advertisement_HasCorrectTable()
    {
        using var context = CreateContext();

        var advertisement = context.Model.FindEntityType(
            typeof(Advertisement));

        Assert.NotNull(advertisement);

        Assert.Equal(
            "Advertisements",
            advertisement.GetTableName());

        Assert.NotNull(
            advertisement.FindPrimaryKey());
    }

    [Fact]
    public void SharedModel_DoesNotDuplicateTableMappings()
    {
        using var context = CreateContext();

        var tables = context.Model.GetEntityTypes()
            .Select(x => x.GetTableName())
            .Where(x => x is not null)
            .ToArray();

        Assert.Equal(
            tables.Length,
            tables.Distinct(StringComparer.OrdinalIgnoreCase)
                .Count());
    }
}
