
using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Wayhunter.Domain.Modules.HomeAdsAnalytics.Entities;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Xunit;

namespace Wayhunter.UnitTests.Modules.HomeAdsAnalytics;

public sealed class AdvertisementConfigurationTests
{
    private static HomeAdsAnalyticsDbContext CreateContext()
    {
        // Only EF model inspection. No SQL connection is opened.
        var options =
            new DbContextOptionsBuilder<HomeAdsAnalyticsDbContext>()
                .UseSqlServer(
                    "Server=localhost;Database=ModelInspectionOnly;Trusted_Connection=True;")
                .Options;

        return new HomeAdsAnalyticsDbContext(options);
    }

    [Fact]
    public void Advertisement_ShouldMapToAdvertisementsTable()
    {
        using var context = CreateContext();

        var entity = context.Model.FindEntityType(
            typeof(Advertisement));

        Assert.NotNull(entity);
        Assert.Equal("Advertisements", entity.GetTableName());

        var primaryKey = entity.FindPrimaryKey();

        Assert.NotNull(primaryKey);
        Assert.Equal(
            nameof(Advertisement.Id),
            Assert.Single(primaryKey.Properties).Name);
    }

    [Fact]
    public void Advertisement_ShouldHaveCorrectColumnConfiguration()
    {
        using var context = CreateContext();

        var entity = context.Model.FindEntityType(
            typeof(Advertisement));

        Assert.NotNull(entity);

        Assert.Equal(
            200,
            entity.FindProperty(nameof(Advertisement.Title))!
                .GetMaxLength());

        Assert.Equal(
            2048,
            entity.FindProperty(nameof(Advertisement.ImageUrl))!
                .GetMaxLength());

        Assert.Equal(
            "datetimeoffset",
            entity.FindProperty(nameof(Advertisement.StartAtUtc))!
                .GetColumnType());

        Assert.Equal(
            "datetimeoffset",
            entity.FindProperty(nameof(Advertisement.EndAtUtc))!
                .GetColumnType());

    }

    [Fact]
    public void Advertisement_Placement_ShouldMapToInteger()
    {
        using var context = CreateContext();

        var entity = context.Model.FindEntityType(
            typeof(Advertisement));

        Assert.NotNull(entity);

        var placement = entity.FindProperty(
            nameof(Advertisement.Placement));

        Assert.NotNull(placement);

        // Placement is stored as an integer
        Assert.Equal(
            typeof(int),
            placement.GetProviderClrType());

        // Placement cannot be null
        Assert.False(placement.IsNullable);
    }

    [Fact]
    public void Advertisement_Status_ShouldMapToInteger()
    {
        using var context = CreateContext();

        var entity = context.Model.FindEntityType(
            typeof(Advertisement));

        Assert.NotNull(entity);

        var status = entity.FindProperty(
            nameof(Advertisement.Status));

        Assert.NotNull(status);

        // Status is stored as an integer
        Assert.Equal(
            typeof(int),
            status.GetProviderClrType());

        // Status cannot be null
        Assert.False(status.IsNullable);
    }
    [Fact]
    public void Advertisement_Counters_ShouldHaveCorrectMapping()
    {
        using var context = CreateContext();

        var entity = context.Model.FindEntityType(
            typeof(Advertisement));

        Assert.NotNull(entity);

        var impression = entity.FindProperty(
            nameof(Advertisement.ImpressionCount));

        var click = entity.FindProperty(
            nameof(Advertisement.ClickCount));

        Assert.NotNull(impression);
        Assert.NotNull(click);

        Assert.Equal("bigint", impression.GetColumnType());
        Assert.Equal(0L, impression.GetDefaultValue());
        Assert.False(impression.IsNullable);

        Assert.Equal("bigint", click.GetColumnType());
        Assert.Equal(0L, click.GetDefaultValue());
        Assert.False(click.IsNullable);
    }


}
