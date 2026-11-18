
using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Wayhunter.Domain.Modules.TripSafety.Entities;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Wayhunter.Infrastructure.Persistence.Models.TripSafety;
using Xunit;

namespace Wayhunter.UnitTests.Modules.TripSafety;

public sealed class TripMembershipRevocationOutboxModelTests
{
    private static WayhunterSharedDbContext CreateContext()
    {
        var options =
            new DbContextOptionsBuilder<WayhunterSharedDbContext>()
                .UseSqlServer(
                    "Server=localhost;" +
                    "Database=Wayhunter_M3_ModelOnly;" +
                    "Trusted_Connection=True;" +
                    "TrustServerCertificate=True")
                .Options;

        return new WayhunterSharedDbContext(options);
    }

    [Fact]
    public void Outbox_Should_Map_To_M3_Table()
    {
        using var context = CreateContext();

        var entity = context.Model.FindEntityType(
            typeof(TripMembershipRevocationOutboxRow));

        Assert.NotNull(entity);

        Assert.Equal(
            "TripSafety_MemberRevocationOutbox",
            entity.GetTableName());
    }

    [Fact]
    public void Outbox_Should_Have_Guid_PrimaryKey()
    {
        using var context = CreateContext();

        var entity = context.Model.FindEntityType(
            typeof(TripMembershipRevocationOutboxRow));

        Assert.NotNull(entity);

        var primaryKey = entity.FindPrimaryKey();

        Assert.NotNull(primaryKey);

        Assert.Equal(
            nameof(TripMembershipRevocationOutboxRow.Id),
            Assert.Single(primaryKey.Properties).Name);
    }

    [Fact]
    public void MemberId_Should_Have_Unique_Index()
    {
        using var context = CreateContext();

        var entity = context.Model.FindEntityType(
            typeof(TripMembershipRevocationOutboxRow));

        Assert.NotNull(entity);

        Assert.Contains(
            entity.GetIndexes(),
            index =>
                index.IsUnique &&
                index.Properties.Count == 1 &&
                index.Properties[0].Name ==
                    nameof(TripMembershipRevocationOutboxRow.MemberId));
    }

    [Fact]
    public void Outbox_Should_Have_TripMember_ForeignKey()
    {
        using var context = CreateContext();

        var entity = context.Model.FindEntityType(
            typeof(TripMembershipRevocationOutboxRow));

        Assert.NotNull(entity);

        Assert.Contains(
            entity.GetForeignKeys(),
            fk =>
                fk.PrincipalEntityType.ClrType ==
                    typeof(TripMember) &&
                fk.Properties.Any(
                    p => p.Name ==
                        nameof(TripMembershipRevocationOutboxRow.MemberId)) &&
                fk.DeleteBehavior == DeleteBehavior.NoAction);
    }

    [Fact]
    public void Outbox_Should_Have_Delivery_Index()
    {
        using var context = CreateContext();

        var entity = context.Model.FindEntityType(
            typeof(TripMembershipRevocationOutboxRow));

        Assert.NotNull(entity);

        Assert.Contains(
            entity.GetIndexes(),
            index =>
                index.Properties.Select(p => p.Name)
                    .SequenceEqual(new[]
                    {
                        nameof(TripMembershipRevocationOutboxRow.IsDelivered),
                        nameof(TripMembershipRevocationOutboxRow.CreatedAtUtc)
                    }));
    }

    [Fact]
    public void Outbox_Should_Have_Check_Constraints()
    {
        using var context = CreateContext();

        var designModel =
            context.GetService<IDesignTimeModel>().Model;

        var entity = designModel.FindEntityType(
            typeof(TripMembershipRevocationOutboxRow));

        Assert.NotNull(entity);

        var constraints =
            entity.GetCheckConstraints().ToList();

        Assert.Contains(
            constraints,
            c => c.Name ==
                "CK_TripSafety_MemberRevocationOutbox_Attempts");

        Assert.Contains(
            constraints,
            c => c.Name ==
                "CK_TripSafety_MemberRevocationOutbox_DeliveryState");
    }

    [Fact]
    public void Outbox_Should_Use_Correct_Sql_Types()
    {
        using var context = CreateContext();

        var entity = context.Model.FindEntityType(
            typeof(TripMembershipRevocationOutboxRow));

        Assert.NotNull(entity);

        Assert.Equal(
            "uniqueidentifier",
            entity.FindProperty(
                nameof(TripMembershipRevocationOutboxRow.MemberId))!
                .GetColumnType());

        Assert.Equal(
            "datetimeoffset",
            entity.FindProperty(
                nameof(TripMembershipRevocationOutboxRow.RemovedAtUtc))!
                .GetColumnType());

        Assert.Equal(
            256,
            entity.FindProperty(
                nameof(TripMembershipRevocationOutboxRow.MemberUserId))!
                .GetMaxLength());
    }

    [Fact]
    public void GeneratedSql_Should_Contain_Outbox_Table()
    {
        using var context = CreateContext();

        var sql = context.Database.GenerateCreateScript();

        Assert.Contains(
            "CREATE TABLE [TripSafety_MemberRevocationOutbox]",
            sql);

        Assert.Contains(
            "UX_TripSafety_MemberRevocationOutbox_MemberId",
            sql);

        Assert.Contains(
            "CK_TripSafety_MemberRevocationOutbox_Attempts",
            sql);

        Assert.Contains(
            "CK_TripSafety_MemberRevocationOutbox_DeliveryState",
            sql);
    }
}
