
using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Wayhunter.Domain.Modules.TravelPlanning.Entities;
using Wayhunter.Domain.Modules.TripSafety.Entities;
using Wayhunter.Infrastructure.Persistence.Configurations.TravelPlanning;
using Xunit;

namespace Wayhunter.UnitTests.Modules.TravelPlanning;

public sealed class TravelPlanningSchemaContractTests
{
    // Builds EF Core metadata only.
    // Does NOT connect to SQL Server.
    private sealed class SchemaTestDbContext : DbContext
    {
        protected override void OnConfiguring(
            DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlServer(
                "Server=localhost;" +
                "Database=Wayhunter_M2_ModelOnly;" +
                "Trusted_Connection=True;" +
                "TrustServerCertificate=True");
        }

        protected override void OnModelCreating(
            ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfiguration(
                new TripPlanConfiguration());

            modelBuilder.ApplyConfiguration(
                new TripStopConfiguration());
        }
    }

    [Fact]
    public void M2_M3_TripPlanIds_ShouldUseGuid()
    {
        Assert.Equal(
            typeof(Guid),
            typeof(TripPlan)
                .GetProperty(nameof(TripPlan.Id))!
                .PropertyType);

        Assert.Equal(
            typeof(Guid),
            typeof(TripStop)
                .GetProperty(nameof(TripStop.TripPlanId))!
                .PropertyType);

        Assert.Equal(
            typeof(Guid),
            typeof(LiveTripSession)
                .GetProperty(
                    nameof(LiveTripSession.TripPlanId))!
                .PropertyType);
    }

    [Fact]
    public void Trips_ShouldHaveCorrectColumnTypes()
    {
        using var db = new SchemaTestDbContext();

        var entity = db.Model.FindEntityType(
            typeof(TripPlan));

        Assert.NotNull(entity);

        var startDate = entity.FindProperty(
            nameof(TripPlan.StartDate));

        var endDate = entity.FindProperty(
            nameof(TripPlan.EndDate));

        var budget = entity.FindProperty(
            nameof(TripPlan.BudgetLkr));

        Assert.NotNull(startDate);
        Assert.NotNull(endDate);
        Assert.NotNull(budget);

        Assert.Equal("date", startDate.GetColumnType());
        Assert.Equal("date", endDate.GetColumnType());
        Assert.Equal(
            "decimal(18,2)",
            budget.GetColumnType());
    }

    [Fact]
    public void Trips_ShouldRequireOwnerAndTripName()
    {
        using var db = new SchemaTestDbContext();

        var entity = db.Model.FindEntityType(
            typeof(TripPlan));

        Assert.NotNull(entity);

        var owner = entity.FindProperty(
            nameof(TripPlan.OwnerUserId));

        var name = entity.FindProperty(
            nameof(TripPlan.TripName));

        Assert.NotNull(owner);
        Assert.NotNull(name);

        Assert.False(owner.IsNullable);
        Assert.False(name.IsNullable);

        Assert.Equal(256, owner.GetMaxLength());
        Assert.Equal(200, name.GetMaxLength());

        Assert.Contains(
            entity.GetIndexes(),
            index => index.Properties.Any(
                property =>
                    property.Name ==
                    nameof(TripPlan.OwnerUserId)));
    }

    [Fact]
    public void TripStops_ShouldKeepOptionalPlaceFields()
    {
        using var db = new SchemaTestDbContext();

        var entity = db.Model.FindEntityType(
            typeof(TripStop));

        Assert.NotNull(entity);

        var placeId = entity.FindProperty(
            nameof(TripStop.PlaceId));

        var latitude = entity.FindProperty(
            nameof(TripStop.Latitude));

        var longitude = entity.FindProperty(
            nameof(TripStop.Longitude));

        Assert.NotNull(placeId);
        Assert.NotNull(latitude);
        Assert.NotNull(longitude);

        Assert.True(placeId.IsNullable);
        Assert.True(latitude.IsNullable);
        Assert.True(longitude.IsNullable);

        Assert.Equal(256, placeId.GetMaxLength());
        Assert.Equal("float", latitude.GetColumnType());
        Assert.Equal("float", longitude.GetColumnType());
    }

    [Fact]
    public void TripStops_ShouldHaveForeignKeyAndUniqueOrder()
    {
        using var db = new SchemaTestDbContext();

        var entity = db.Model.FindEntityType(
            typeof(TripStop));

        Assert.NotNull(entity);

        var foreignKey = Assert.Single(
            entity.GetForeignKeys());

        Assert.Equal(
            typeof(TripPlan),
            foreignKey.PrincipalEntityType.ClrType);

        Assert.Equal(
            nameof(TripStop.TripPlanId),
            Assert.Single(foreignKey.Properties).Name);

        Assert.Equal(
            DeleteBehavior.Cascade,
            foreignKey.DeleteBehavior);

        Assert.Contains(
            entity.GetIndexes(),
            index =>
                index.IsUnique &&
                index.Properties.Count == 2 &&
                index.Properties[0].Name ==
                    nameof(TripStop.TripPlanId) &&
                index.Properties[1].Name ==
                    nameof(TripStop.OrderIndex));
    }
}
