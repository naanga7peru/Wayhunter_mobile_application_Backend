using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Xunit;
using Xunit.Abstractions;

namespace Wayhunter.UnitTests.Modules.TripSafety;

public sealed class M3FinalSqlMetadataTests(ITestOutputHelper output)
{
    [Fact]
    public void CompareSharedDesignModelWithBothCheckedInBaselinesWithoutOpeningSql()
    {
        using var db = new WayhunterSharedDbContext(new DbContextOptionsBuilder<WayhunterSharedDbContext>()
            .UseSqlServer("Server=localhost;Database=MetadataOnly;Integrated Security=True").Options);
        var migrations = db.GetService<IMigrationsAssembly>();
        var migration = migrations.CreateMigration(migrations.Migrations["20261117093023_InitialSharedSchema"],
            db.Database.ProviderName!);
        var initializer = db.GetService<IModelRuntimeInitializer>();
        var model = db.GetService<IDesignTimeModel>().Model.GetRelationalModel();
        var snapshot = initializer.Initialize(migrations.ModelSnapshot!.Model).GetRelationalModel();
        var initial = initializer.Initialize(migration.TargetModel).GetRelationalModel();

        var snapshotEvidence = DescribeM3(snapshot);
        Assert.Equal(snapshotEvidence, DescribeM3(initial));
        foreach (var line in DescribeM3(model).Except(snapshotEvidence))
            output.WriteLine(line);

        Assert.NotNull(snapshot.FindTable("TripSafety_LiveTripSessions", null));
        Assert.NotNull(snapshot.FindTable("TripSafety_GpsConsents", null));
        Assert.All(new[] { "TripSafety_SafeCheckIns", "TripSafety_SafeCheckInOutbox",
            "TripSafety_MemberRevocationOutbox", "TripSafety_TripMemories" }, table =>
        {
            Assert.Null(snapshot.FindTable(table, null));
            Assert.NotNull(model.FindTable(table, null));
        });
        Assert.Equal(System.Data.ConnectionState.Closed, db.Database.GetDbConnection().State);
    }

    private static string[] DescribeM3(IRelationalModel model) =>
        model.Tables.Where(table => table.Name.StartsWith("TripSafety_", StringComparison.Ordinal) ||
                table.Name == "TripStops")
            .SelectMany(table => new[] { $"{table.Name} TABLE" }
                .Concat(table.Columns.Select(column => $"{table.Name} COLUMN {column.Name} {column.StoreType}"))
                .Concat(table.UniqueConstraints.Select(key =>
                    $"{table.Name} KEY {key.Name} ({string.Join(",", key.Columns.Select(column => column.Name))})"))
                .Concat(table.Indexes.Select(index =>
                    $"{table.Name} INDEX {index.Name} ({string.Join(",", index.Columns.Select(column => column.Name))}) unique={index.IsUnique} filter={index.Filter}"))
                .Concat(table.ForeignKeyConstraints.Select(key =>
                    $"{table.Name} FK {key.Name} ({string.Join(",", key.Columns.Select(column => column.Name))}) -> {key.PrincipalTable.Name}({string.Join(",", key.PrincipalColumns.Select(column => column.Name))}) {key.OnDeleteAction}"))
                .Concat(table.CheckConstraints.Select(check => $"{table.Name} CHECK {check.Name}: {check.Sql}")))
            .Order(StringComparer.Ordinal).ToArray();
}
