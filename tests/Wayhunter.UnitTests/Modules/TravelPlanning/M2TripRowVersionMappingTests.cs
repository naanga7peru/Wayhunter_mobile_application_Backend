
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Wayhunter.Domain.Modules.TravelPlanning.Entities;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Xunit;

namespace Wayhunter.UnitTests.Modules.TravelPlanning;

public sealed class M2TripRowVersionMappingTests
{
    [Fact]
    public void TripPlan_RowVersion_ShouldBeConcurrencyToken()
    {
        var options =
            new DbContextOptionsBuilder<WayhunterSharedDbContext>()
                .UseSqlServer(
                    "Server=localhost;" +
                    "Database=Wayhunter_ModelOnly;" +
                    "Trusted_Connection=True;" +
                    "TrustServerCertificate=True")
                .Options;

        using var db = new WayhunterSharedDbContext(options);

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
}
