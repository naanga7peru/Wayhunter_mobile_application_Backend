
using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Wayhunter.Domain.Modules.HomeAdsAnalytics.Entities;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Xunit;

namespace Wayhunter.UnitTests.Modules.HomeAdsAnalytics;

public sealed class AdvertisementConfigurationTests
{
    // Model inspection only.
    // No SQL Server connection is opened.
    private const string ModelConnection =
        "Server=localhost;Database=ModelInspectionOnly;Trusted_Connection=True;";

    private static WayhunterSharedDbContext CreateSharedContext()
    {
        var options =
            new DbContextOptionsBuilder<WayhunterSharedDbContext>()
                .UseSqlServer(ModelConnection)
                .Options;

        return new WayhunterSharedDbContext(options);
    }

    // TEST 01 - Advertisement Table and Primary Key
    [Fact]
    public void Advertisement_ShouldMapToAdvertisementsTable()
    {
        using var context = CreateSharedContext();

        var entity = context.Model.FindEntityType(
            typeof(Advertisement));

        Assert.NotNull(entity);

        Assert.Equal(
            "Advertisements",
            entity.GetTableName());

        var primaryKey = entity.FindPrimaryKey();

        Assert.NotNull(primaryKey);

        Assert.Equal(
            nameof(Advertisement.Id),
            Assert.Single(primaryKey.Properties).Name);
    }

    // TEST 02 - Advertisement Column Configuration
    [Fact]
    public void Advertisement_ShouldHaveCorrectColumnConfiguration()
    {
        using var context = CreateSharedContext();

        var entity = context.Model.FindEntityType(
            typeof(Advertisement));

        Assert.NotNull(entity);

        Assert.Equal(
            200,
            entity.FindProperty(
                nameof(Advertisement.Title))!
                .GetMaxLength());

        Assert.Equal(
            2048,
            entity.FindProperty(
                nameof(Advertisement.ImageUrl))!
                .GetMaxLength());

        Assert.Equal(
            "datetimeoffset",
            entity.FindProperty(
                nameof(Advertisement.StartAtUtc))!
                .GetColumnType());

        Assert.Equal(
            "datetimeoffset",
            entity.FindProperty(
                nameof(Advertisement.EndAtUtc))!
                .GetColumnType());
    }

    // TEST 03 - Placement Integer Mapping
    [Fact]
    public void Advertisement_Placement_ShouldMapToInteger()
    {
        using var context = CreateSharedContext();

        var entity = context.Model.FindEntityType(
            typeof(Advertisement));

        Assert.NotNull(entity);

        var placement = entity.FindProperty(
            nameof(Advertisement.Placement));

        Assert.NotNull(placement);

        Assert.Equal(
            typeof(int),
            placement.GetProviderClrType());

        Assert.False(placement.IsNullable);
    }

    // TEST 04 - Status Integer Mapping
    [Fact]
    public void Advertisement_Status_ShouldMapToInteger()
    {
        using var context = CreateSharedContext();

        var entity = context.Model.FindEntityType(
            typeof(Advertisement));

        Assert.NotNull(entity);

        var status = entity.FindProperty(
            nameof(Advertisement.Status));

        Assert.NotNull(status);

        Assert.Equal(
            typeof(int),
            status.GetProviderClrType());

        Assert.False(status.IsNullable);
    }

    // TEST 05 - Impression and Click Counters
    [Fact]
    public void Advertisement_Counters_ShouldHaveCorrectMapping()
    {
        using var context = CreateSharedContext();

        var entity = context.Model.FindEntityType(
            typeof(Advertisement));

        Assert.NotNull(entity);

        var impression = entity.FindProperty(
            nameof(Advertisement.ImpressionCount));

        var click = entity.FindProperty(
            nameof(Advertisement.ClickCount));

        Assert.NotNull(impression);
        Assert.NotNull(click);

        Assert.Equal(
            "bigint",
            impression.GetColumnType());

        Assert.Equal(
            0L,
            impression.GetDefaultValue());

        Assert.False(impression.IsNullable);

        Assert.Equal(
            "bigint",
            click.GetColumnType());

        Assert.Equal(
            0L,
            click.GetDefaultValue());

        Assert.False(click.IsNullable);
    }

    // TEST 06 - Active Advertisement Index
    [Fact]
    public void Advertisement_ShouldHaveActiveWindowIndex()
    {
        using var context = CreateSharedContext();

        var entity = context.Model.FindEntityType(
            typeof(Advertisement));

        Assert.NotNull(entity);

        var index = Assert.Single(
            entity.GetIndexes(),
            candidate =>
                candidate.GetDatabaseName() ==
                "IX_Advertisements_Enabled_Window");

        Assert.Equal(
            new[]
            {
                nameof(Advertisement.IsEnabled),
                nameof(Advertisement.StartAtUtc),
                nameof(Advertisement.EndAtUtc)
            },
            index.Properties.Select(
                property => property.Name));
    }

    // TEST 07 - Valid SQL Date Window Constraint
    // EF Core 10 requires the design-time model.
    [Fact]
    public void Advertisement_ShouldHaveValidWindowConstraint()
    {
        using var context = CreateSharedContext();

        var designTimeModel = context
            .GetService<IDesignTimeModel>()
            .Model;

        var entity = designTimeModel.FindEntityType(
            typeof(Advertisement));

        Assert.NotNull(entity);

        var constraint = Assert.Single(
            entity.GetCheckConstraints(),
            candidate =>
                candidate.Name ==
                "CK_Advertisements_ValidWindow");

        Assert.Equal(
            "[EndAtUtc] > [StartAtUtc]",
            constraint.Sql);
    }

    // TEST 08 - Shared DbContext Advertisement Mapping
    // Verifies shared migration model readiness only.
    [Fact]
    public void SharedContext_ShouldIncludeAdvertisementMapping()
    {
        using var context = CreateSharedContext();

        var designTimeModel = context
            .GetService<IDesignTimeModel>()
            .Model;

        var entity = designTimeModel.FindEntityType(
            typeof(Advertisement));

        Assert.NotNull(entity);

        Assert.Equal(
            "Advertisements",
            entity.GetTableName());

        var primaryKey = entity.FindPrimaryKey();

        Assert.NotNull(primaryKey);

        Assert.Equal(
            nameof(Advertisement.Id),
            Assert.Single(primaryKey.Properties).Name);

        var constraint = Assert.Single(
            entity.GetCheckConstraints(),
            candidate =>
                candidate.Name ==
                "CK_Advertisements_ValidWindow");

        Assert.Equal(
            "[EndAtUtc] > [StartAtUtc]",
            constraint.Sql);

        Assert.Contains(
            entity.GetIndexes(),
            index =>
                index.GetDatabaseName() ==
                "IX_Advertisements_Enabled_Window");
    }

    // TEST 09 - Approved Primary Key Name and SQL Type
    [Fact]
    public void Advertisement_ShouldHaveApprovedPrimaryKey()
    {
        using var context = CreateSharedContext();

        var entity = context.Model.FindEntityType(
            typeof(Advertisement));

        Assert.NotNull(entity);

        var key = entity.FindPrimaryKey();

        Assert.NotNull(key);

        Assert.Equal("PK_Advertisements", key.GetName());

        var id = entity.FindProperty(
            nameof(Advertisement.Id));

        Assert.NotNull(id);
        Assert.Equal("uniqueidentifier", id.GetColumnType());
        Assert.Equal(ValueGenerated.Never, id.ValueGenerated);
        Assert.False(id.IsNullable);
    }

    // TEST 10 - Approved Placement CHECK Constraint
    [Fact]
    public void Advertisement_ShouldHavePlacementConstraint()
    {
        using var context = CreateSharedContext();

        var model = context
            .GetService<IDesignTimeModel>()
            .Model;

        var entity = model.FindEntityType(
            typeof(Advertisement));

        Assert.NotNull(entity);

        var constraint = Assert.Single(
            entity.GetCheckConstraints(),
            candidate =>
                candidate.Name ==
                "CK_Advertisements_Placement");

        Assert.Equal(
            "[Placement] IN (1, 2)",
            constraint.Sql);
    }

    // TEST 11 - Approved Status CHECK Constraint
    [Fact]
    public void Advertisement_ShouldHaveStatusConstraint()
    {
        using var context = CreateSharedContext();

        var model = context
            .GetService<IDesignTimeModel>()
            .Model;

        var entity = model.FindEntityType(
            typeof(Advertisement));

        Assert.NotNull(entity);

        var constraint = Assert.Single(
            entity.GetCheckConstraints(),
            candidate =>
                candidate.Name ==
                "CK_Advertisements_Status");

        Assert.Equal(
            "[Status] IN (0, 1, 2, 3)",
            constraint.Sql);
    }

    // TEST 12 - Shared Model Contains Exactly Three Constraints
    [Fact]
    public void SharedContext_ShouldHaveThreeAdvertisementConstraints()
    {
        using var context = CreateSharedContext();

        var model = context
            .GetService<IDesignTimeModel>()
            .Model;

        var entity = model.FindEntityType(
            typeof(Advertisement));

        Assert.NotNull(entity);

        var constraints = entity.GetCheckConstraints().ToList();

        Assert.Equal(3, constraints.Count);

        Assert.Contains(
            constraints,
            c => c.Name == "CK_Advertisements_ValidWindow");

        Assert.Contains(
            constraints,
            c => c.Name == "CK_Advertisements_Placement");

        Assert.Contains(
            constraints,
            c => c.Name == "CK_Advertisements_Status");
    }

    // TEST 13 - Advertisement Registered Once in Shared Model
    [Fact]
    public void SharedContext_ShouldRegisterAdvertisementExactlyOnce()
    {
        using var context = CreateSharedContext();

        var matches = context.Model
            .GetEntityTypes()
            .Where(entity =>
                entity.ClrType == typeof(Advertisement))
            .ToList();

        Assert.Single(matches);
    }

}
