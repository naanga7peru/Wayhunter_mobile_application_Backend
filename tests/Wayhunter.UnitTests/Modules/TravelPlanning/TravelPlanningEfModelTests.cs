
using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Wayhunter.Domain.Modules.TravelPlanning.Entities;
using Wayhunter.Infrastructure.Persistence.Configurations.TravelPlanning;
using Xunit;

namespace Wayhunter.UnitTests.Modules.TravelPlanning;

public sealed class TravelPlanningEfModelTests
{
    // Only builds EF Core model metadata.
    // No actual SQL Server connection is made.
    private sealed class ModelOnlyDbContext : DbContext
    {
        protected override void OnConfiguring(
            DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlServer(
                "Server=localhost;" +
                "Database=Wayhunter_Model_Only;" +
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
    public void Trips_ShouldHavePrimaryKeyAndOwnerIndex()
    {
        using var context = new ModelOnlyDbContext();

        var trip = context.Model.FindEntityType(
            typeof(TripPlan));

        Assert.NotNull(trip);

        Assert.Equal("Trips", trip.GetTableName());

        var primaryKey = trip.FindPrimaryKey();

        Assert.NotNull(primaryKey);
        Assert.Equal(
            nameof(TripPlan.Id),
            Assert.Single(primaryKey.Properties).Name);

        Assert.Contains(
            trip.GetIndexes(),
            index => index.Properties.Any(
                p => p.Name == nameof(TripPlan.OwnerUserId)));
    }

    [Fact]
    public void TripStops_ShouldHaveCorrectForeignKey()
    {
        using var context = new ModelOnlyDbContext();

        var trip = context.Model.FindEntityType(
            typeof(TripPlan));

        var stop = context.Model.FindEntityType(
            typeof(TripStop));

        Assert.NotNull(trip);
        Assert.NotNull(stop);

        Assert.Equal("TripStops", stop.GetTableName());

        var foreignKey = Assert.Single(
            stop.GetForeignKeys());

        Assert.Equal(
            nameof(TripStop.TripPlanId),
            Assert.Single(foreignKey.Properties).Name);

        Assert.Equal(
            trip,
            foreignKey.PrincipalEntityType);

        Assert.Equal(
            DeleteBehavior.Cascade,
            foreignKey.DeleteBehavior);
    }

    [Fact]
    public void TripStops_ShouldHaveUniqueOrderPerTrip()
    {
        using var context = new ModelOnlyDbContext();

        var stop = context.Model.FindEntityType(
            typeof(TripStop));

        Assert.NotNull(stop);

        Assert.Contains(
            stop.GetIndexes(),
            index =>
                index.IsUnique &&
                index.Properties.Count == 2 &&
                index.Properties[0].Name ==
                    nameof(TripStop.TripPlanId) &&
                index.Properties[1].Name ==
                    nameof(TripStop.OrderIndex));
    }
}
