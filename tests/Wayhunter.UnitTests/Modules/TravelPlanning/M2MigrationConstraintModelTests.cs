
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Wayhunter.Domain.Modules.TravelPlanning.Entities;
using Wayhunter.Infrastructure.Persistence.Configurations.IdentitySocial;
using Wayhunter.Infrastructure.Persistence.Configurations.TravelPlanning;
using Xunit;

namespace Wayhunter.UnitTests.Modules.TravelPlanning;

public sealed class M2MigrationConstraintModelTests
{
    // EF metadata-only context.
    // No SQL connection or migration is executed.
    private sealed class ModelOnlyContext : DbContext
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
            base.OnModelCreating(modelBuilder);

            // Required for M1 -> M2 Owner FK.
            modelBuilder.ApplyConfiguration(
                new UserAccountConfiguration());

            // M2 table mappings.
            modelBuilder.ApplyConfiguration(
                new TripPlanConfiguration());

            modelBuilder.ApplyConfiguration(
                new TripStopConfiguration());
        }
    }

    // Trips table constraints.
    [Theory]
    [InlineData("CK_Trips_DateRange")]
    [InlineData("CK_Trips_Budget")]
    [InlineData("CK_Trips_Headcount")]
    public void Trips_ShouldHaveRequiredConstraints(
        string constraintName)
    {
        using var db = new ModelOnlyContext();

        // CHECK constraints require design-time metadata.
        var model = db.GetService<IDesignTimeModel>().Model;

        var entity = model.FindEntityType(
            typeof(TripPlan));

        Assert.NotNull(entity);

        Assert.Contains(
            entity.GetCheckConstraints(),
            constraint =>
                constraint.Name == constraintName);
    }

    // TripStops table constraints.
    [Theory]
    [InlineData("CK_TripStops_OrderIndex")]
    [InlineData("CK_TripStops_Coordinates")]
    public void TripStops_ShouldHaveRequiredConstraints(
        string constraintName)
    {
        using var db = new ModelOnlyContext();

        var model = db.GetService<IDesignTimeModel>().Model;

        var entity = model.FindEntityType(
            typeof(TripStop));

        Assert.NotNull(entity);

        Assert.Contains(
            entity.GetCheckConstraints(),
            constraint =>
                constraint.Name == constraintName);
    }
}
