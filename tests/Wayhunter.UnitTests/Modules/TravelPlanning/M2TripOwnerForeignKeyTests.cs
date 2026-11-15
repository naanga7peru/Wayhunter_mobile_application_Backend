
using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Wayhunter.Domain.Modules.TravelPlanning.Entities;
using Wayhunter.Infrastructure.Persistence.Configurations.IdentitySocial;
using Wayhunter.Infrastructure.Persistence.Configurations.TravelPlanning;
using Xunit;

namespace Wayhunter.UnitTests.Modules.TravelPlanning;

public sealed class M2TripOwnerForeignKeyTests
{
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
            modelBuilder.ApplyConfiguration(
                new UserAccountConfiguration());

            modelBuilder.ApplyConfiguration(
                new TripPlanConfiguration());

            modelBuilder.ApplyConfiguration(
                new TripStopConfiguration());
        }
    }

    [Fact]
    public void TripOwner_ShouldReferenceUsers()
    {
        using var db = new ModelOnlyContext();

        var trip = db.Model.FindEntityType(
            typeof(TripPlan));

        Assert.NotNull(trip);

        var foreignKey = Assert.Single(
            trip.GetForeignKeys(),
            fk => fk.PrincipalEntityType.ClrType ==
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
}
