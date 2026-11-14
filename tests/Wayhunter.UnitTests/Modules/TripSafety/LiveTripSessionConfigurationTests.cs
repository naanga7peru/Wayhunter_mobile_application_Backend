
using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Wayhunter.Domain.Modules.TripSafety.Entities;
using Wayhunter.Domain.Modules.TripSafety.Enums;
using Wayhunter.Infrastructure.Persistence.Configurations.TripSafety;
using Xunit;

namespace Wayhunter.UnitTests.Modules.TripSafety;

public sealed class LiveTripSessionConfigurationTests
{
    // This context is ONLY for checking EF Core model metadata.
    // No database connection or migration is executed.
    private sealed class MappingTestDbContext : DbContext
    {
        public MappingTestDbContext(
            DbContextOptions<MappingTestDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(
            ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfiguration(
                new LiveTripSessionConfiguration());
        }
    }

    private static MappingTestDbContext CreateContext()
    {
        var options =
            new DbContextOptionsBuilder<MappingTestDbContext>()
                .UseSqlServer(
                    "Server=localhost;" +
                    "Database=Wayhunter_M3_ModelOnly;" +
                    "Trusted_Connection=True;" +
                    "TrustServerCertificate=True")
                .Options;

        return new MappingTestDbContext(options);
    }

    [Fact]
    public void Should_Map_Correct_Table_Name()
    {
        using var context = CreateContext();

        var entity = context.Model.FindEntityType(
            typeof(LiveTripSession));

        Assert.NotNull(entity);

        Assert.Equal(
            "TripSafety_LiveTripSessions",
            entity.GetTableName());
    }

    [Fact]
    public void Should_Configure_SessionId_As_Primary_Key()
    {
        using var context = CreateContext();

        var entity = context.Model.FindEntityType(
            typeof(LiveTripSession));

        Assert.NotNull(entity);

        var primaryKey = entity.FindPrimaryKey();

        Assert.NotNull(primaryKey);

        var keyProperty = Assert.Single(
            primaryKey.Properties);

        Assert.Equal(
            nameof(LiveTripSession.SessionId),
            keyProperty.Name);
    }

    [Fact]
    public void Should_Configure_TripPlanId_As_Required()
    {
        using var context = CreateContext();

        var entity = context.Model.FindEntityType(
            typeof(LiveTripSession));

        Assert.NotNull(entity);

        var property = entity.FindProperty(
            nameof(LiveTripSession.TripPlanId));

        Assert.NotNull(property);
        Assert.False(property.IsNullable);
        Assert.Equal(128, property.GetMaxLength());
    }

    [Fact]
    public void Should_Map_Timestamps_As_Datetimeoffset()
    {
        using var context = CreateContext();

        var entity = context.Model.FindEntityType(
            typeof(LiveTripSession));

        Assert.NotNull(entity);

        var started = entity.FindProperty(
            nameof(LiveTripSession.StartedAtUtc));

        var finished = entity.FindProperty(
            nameof(LiveTripSession.FinishedAtUtc));

        Assert.NotNull(started);
        Assert.NotNull(finished);

        Assert.Equal(
            "datetimeoffset",
            started.GetColumnType());

        Assert.Equal(
            "datetimeoffset",
            finished.GetColumnType());

        Assert.False(started.IsNullable);
        Assert.True(finished.IsNullable);
    }

    [Fact]
    public void Should_Configure_Filtered_Unique_Index()
    {
        using var context = CreateContext();

        var entity = context.Model.FindEntityType(
            typeof(LiveTripSession));

        Assert.NotNull(entity);

        var index = Assert.Single(entity.GetIndexes());

        Assert.True(index.IsUnique);

        Assert.Equal(
            nameof(LiveTripSession.TripPlanId),
            Assert.Single(index.Properties).Name);

        Assert.Equal(
            "UX_TripSafety_LiveTripSessions_OpenPlan",
            index.GetDatabaseName());

        Assert.Equal(
            "[Status] IN (1, 2)",
            index.GetFilter());
    }
}
