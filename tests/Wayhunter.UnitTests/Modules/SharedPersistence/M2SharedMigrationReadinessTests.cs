
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using System;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Wayhunter.Domain.Modules.TravelPlanning.Entities;
using Wayhunter.Domain.Modules.TripSafety.Entities;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Xunit;

namespace Wayhunter.UnitTests.Modules.SharedPersistence;

public sealed class M2SharedMigrationReadinessTests
{
    // Tests the real shared EF model.
    // No SQL Server connection is opened.
    // No migration is executed.
    private static WayhunterSharedDbContext CreateContext()
    {
        var options =
            new DbContextOptionsBuilder<WayhunterSharedDbContext>()
                .UseSqlServer(
                    "Server=localhost;" +
                    "Database=Wayhunter_M2_ModelOnly;" +
                    "Trusted_Connection=True;" +
                    "TrustServerCertificate=True")
                .Options;

        return new WayhunterSharedDbContext(options);
    }

    // Verify all five proposed M2 SQL CHECK constraints.
    [Theory]
    [InlineData(typeof(TripPlan), "CK_Trips_DateRange")]
    [InlineData(typeof(TripPlan), "CK_Trips_Budget")]
    [InlineData(typeof(TripPlan), "CK_Trips_Headcount")]
    [InlineData(typeof(TripStop), "CK_TripStops_OrderIndex")]
    [InlineData(typeof(TripStop), "CK_TripStops_Coordinates")]
    public void SharedModel_ShouldContainM2CheckConstraint(
        Type entityType,
        string constraintName)
    {
        using var db = CreateContext();

        var model =
            db.GetService<IDesignTimeModel>().Model;

        var entity = model.FindEntityType(entityType);

        Assert.NotNull(entity);

        Assert.Contains(
            entity.GetCheckConstraints(),
            constraint =>
                constraint.Name == constraintName);
    }

    // M1 Users -> M2 Trips relationship.
    [Fact]
    public void SharedModel_ShouldHaveTripOwnerForeignKey()
    {
        using var db = CreateContext();

        var model =
            db.GetService<IDesignTimeModel>().Model;

        var trip = model.FindEntityType(typeof(TripPlan));

        Assert.NotNull(trip);

        var foreignKey = Assert.Single(
            trip.GetForeignKeys(),
            fk =>
                fk.PrincipalEntityType.ClrType ==
                typeof(UserAccount));

        Assert.Equal(
            nameof(TripPlan.OwnerUserId),
            Assert.Single(foreignKey.Properties).Name);

        Assert.Equal(
            nameof(UserAccount.UserId),
            Assert.Single(
                foreignKey.PrincipalKey.Properties).Name);

        Assert.Equal(
            DeleteBehavior.NoAction,
            foreignKey.DeleteBehavior);

        Assert.Equal(
            "FK_Trips_Users_OwnerUserId",
            foreignKey.GetConstraintName());
    }

    // M2 Trips -> TripStops relationship.
    [Fact]
    public void SharedModel_ShouldHaveTripStopsForeignKey()
    {
        using var db = CreateContext();

        var stop = db.Model.FindEntityType(
            typeof(TripStop));

        Assert.NotNull(stop);

        var foreignKey = Assert.Single(
            stop.GetForeignKeys(),
            fk =>
                fk.PrincipalEntityType.ClrType ==
                typeof(TripPlan));

        Assert.Equal(
            nameof(TripStop.TripPlanId),
            Assert.Single(foreignKey.Properties).Name);

        Assert.Equal(
            DeleteBehavior.Cascade,
            foreignKey.DeleteBehavior);
    }

    // M3 LiveTripSession -> M2 Trips relationship.
    [Fact]
    public void SharedModel_ShouldHaveM3TripForeignKey()
    {
        using var db = CreateContext();

        var session = db.Model.FindEntityType(
            typeof(LiveTripSession));

        Assert.NotNull(session);

        var foreignKey = Assert.Single(
            session.GetForeignKeys(),
            fk =>
                fk.PrincipalEntityType.ClrType ==
                typeof(TripPlan));

        Assert.Equal(
            nameof(LiveTripSession.TripPlanId),
            Assert.Single(foreignKey.Properties).Name);

        Assert.Equal(
            DeleteBehavior.NoAction,
            foreignKey.DeleteBehavior);
    }
}
