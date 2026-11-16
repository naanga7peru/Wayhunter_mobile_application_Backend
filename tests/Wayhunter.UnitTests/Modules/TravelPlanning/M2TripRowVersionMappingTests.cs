using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Wayhunter.Domain.Modules.TravelPlanning.Entities;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Xunit;

namespace Wayhunter.UnitTests.Modules.TravelPlanning;

public sealed class M2TripRowVersionMappingTests
{
    private static WayhunterSharedDbContext CreateDbContext()
    {
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
    public void TripPlan_RowVersion_ShouldBeConcurrencyToken()
    {
        using var db = CreateDbContext();

        var trip = db.Model.FindEntityType(typeof(TripPlan));

        Assert.NotNull(trip);

        var rowVersion = trip.FindProperty(
            nameof(TripPlan.RowVersion));

        Assert.NotNull(rowVersion);

        Assert.Equal(typeof(byte[]), rowVersion.ClrType);

        Assert.True(rowVersion.IsConcurrencyToken);

        Assert.Equal(
            ValueGenerated.OnAddOrUpdate,
            rowVersion.ValueGenerated);
    }

    [Fact]
    public void TripPlan_ShouldMapToTripsTable()
    {
        using var db = CreateDbContext();

        var trip = db.Model.FindEntityType(typeof(TripPlan));

        Assert.NotNull(trip);
        Assert.Equal("Trips", trip.GetTableName());

        var primaryKey = trip.FindPrimaryKey();

        Assert.NotNull(primaryKey);

        Assert.Contains(
            primaryKey.Properties,
            property => property.Name == nameof(TripPlan.Id));
    }

    [Fact]
    public void TripPlan_OwnerForeignKey_ShouldReferenceUserAccount()
    {
        using var db = CreateDbContext();

        var trip = db.Model.FindEntityType(typeof(TripPlan));

        Assert.NotNull(trip);

        var foreignKey = trip.GetForeignKeys()
            .Single(fk =>
                fk.Properties.Any(property =>
                    property.Name == nameof(TripPlan.OwnerUserId)));

        Assert.Equal(
            typeof(UserAccount),
            foreignKey.PrincipalEntityType.ClrType);

        Assert.Equal(
            nameof(UserAccount.UserId),
            Assert.Single(
                foreignKey.PrincipalKey.Properties).Name);

        Assert.Equal(
            DeleteBehavior.NoAction,
            foreignKey.DeleteBehavior);

        var ownerProperty = Assert.Single(
            foreignKey.Properties);

        var userIdProperty = Assert.Single(
            foreignKey.PrincipalKey.Properties);

        Assert.Equal(
            ownerProperty.ClrType,
            userIdProperty.ClrType);

        Assert.Equal(256, ownerProperty.GetMaxLength());
        Assert.Equal(256, userIdProperty.GetMaxLength());
    }

    [Fact]
    public void TripStop_ShouldMapToTripStopsTable()
    {
        using var db = CreateDbContext();

        var stop = db.Model.FindEntityType(typeof(TripStop));

        Assert.NotNull(stop);

        Assert.Equal(
            "TripStops",
            stop.GetTableName());

        var primaryKey = stop.FindPrimaryKey();

        Assert.NotNull(primaryKey);

        Assert.Equal(
            nameof(TripStop.Id),
            Assert.Single(primaryKey.Properties).Name);
    }

    [Fact]
    public void TripStop_OrderIndex_ShouldBeUniqueWithinTrip()
    {
        using var db = CreateDbContext();

        var stop = db.Model.FindEntityType(typeof(TripStop));

        Assert.NotNull(stop);

        var uniqueIndex = stop.GetIndexes()
            .Single(index =>
                index.Properties.Select(property => property.Name)
                    .SequenceEqual(new[]
                    {
                        nameof(TripStop.TripPlanId),
                        nameof(TripStop.OrderIndex)
                    }));

        Assert.True(uniqueIndex.IsUnique);
    }

    [Fact]
    public void TripStop_ForeignKey_ShouldCascadeOnPhysicalDelete()
    {
        using var db = CreateDbContext();

        var stop = db.Model.FindEntityType(typeof(TripStop));

        Assert.NotNull(stop);

        var foreignKey = stop.GetForeignKeys()
            .Single(fk =>
                fk.Properties.Any(property =>
                    property.Name == nameof(TripStop.TripPlanId)));

        Assert.Equal(
            typeof(TripPlan),
            foreignKey.PrincipalEntityType.ClrType);

        Assert.Equal(
            DeleteBehavior.Cascade,
            foreignKey.DeleteBehavior);
    }

    [Fact]
    public void TripPlan_ShouldHaveExpectedCheckConstraints()
    {
        using var db = CreateDbContext();

        var model = db.GetService<IDesignTimeModel>().Model;
        var trip = model.FindEntityType(typeof(TripPlan));

        Assert.NotNull(trip);

        var constraintNames = trip.GetCheckConstraints()
            .Select(constraint => constraint.Name)
            .ToArray();

        Assert.Contains(
            "CK_Trips_DateRange",
            constraintNames);

        Assert.Contains(
            "CK_Trips_Budget",
            constraintNames);

        Assert.Contains(
            "CK_Trips_Headcount",
            constraintNames);
    }

    [Fact]
    public void TripStop_ShouldHaveExpectedCheckConstraints()
    {
        using var db = CreateDbContext();

        var model = db.GetService<IDesignTimeModel>().Model;
        var stop = model.FindEntityType(typeof(TripStop));

        Assert.NotNull(stop);

        var constraintNames = stop.GetCheckConstraints()
            .Select(constraint => constraint.Name)
            .ToArray();

        Assert.Contains(
            "CK_TripStops_OrderIndex",
            constraintNames);

        Assert.Contains(
            "CK_TripStops_Coordinates",
            constraintNames);
    }
}
