using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Wayhunter.Application.Modules.TripSafety.Memories;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Wayhunter.Domain.Modules.TravelPlanning.Entities;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Wayhunter.Infrastructure.Persistence.Models.TripSafety;
using Xunit;

namespace Wayhunter.UnitTests.Modules.TripSafety;

public sealed class M3Ms07TripMemoryMetadataTests
{
    [Fact]
    public void ProposedModelEnforcesOwnershipBindingAndUniqueOperationKeys()
    {
        using var db = new WayhunterSharedDbContext(new DbContextOptionsBuilder<WayhunterSharedDbContext>()
            .UseSqlServer("Server=localhost;Database=MS07ModelOnly;Integrated Security=True").Options);
        var model = db.GetService<IDesignTimeModel>().Model;
        var memory = model.FindEntityType(typeof(TripMemoryRow))!;
        var media = model.FindEntityType(typeof(TripMemoryMediaRow))!;

        Assert.Equal("TripSafety_TripMemories", memory.GetTableName());
        Assert.Equal("MemoryId", Assert.Single(memory.FindPrimaryKey()!.Properties).Name);
        Assert.Equal("TripSafety_TripMemoryMedia", media.GetTableName());
        Assert.Equal("OperationId", Assert.Single(media.FindPrimaryKey()!.Properties).Name);
        Assert.Equal(TripMemoryVisibility.Private, memory.FindProperty("Visibility")!.GetDefaultValue());
        Assert.Equal("nvarchar(256)", memory.FindProperty("OwnerUserId")!.GetColumnType());
        Assert.Equal("datetimeoffset", memory.FindProperty("CreatedAtUtc")!.GetColumnType());
        Assert.True(memory.FindProperty("DeletedAtUtc")!.IsNullable);

        var stop = Assert.Single(memory.GetForeignKeys(), x => x.PrincipalEntityType.ClrType == typeof(TripStop));
        Assert.Equal(new[] { "TripStopId", "TripPlanId" }, stop.Properties.Select(x => x.Name));
        Assert.Equal(new[] { "Id", "TripPlanId" }, stop.PrincipalKey.Properties.Select(x => x.Name));
        Assert.False(stop.IsRequired);
        Assert.Equal(DeleteBehavior.ClientNoAction, stop.DeleteBehavior);
        Assert.Equal(ReferentialAction.NoAction, Assert.Single(stop.GetMappedConstraints()).OnDeleteAction);
        Assert.Contains(memory.GetForeignKeys(), x => x.PrincipalEntityType.ClrType == typeof(UserAccount));
        Assert.Contains(memory.GetForeignKeys(), x => x.PrincipalEntityType.ClrType == typeof(TripPlan));
        Assert.All(memory.GetForeignKeys().Where(fk => fk != stop).Concat(media.GetForeignKeys()),
            fk => Assert.Equal(DeleteBehavior.NoAction, fk.DeleteBehavior));
        Assert.True(Assert.Single(media.GetForeignKeys()).IsUnique);

        Assert.Contains(memory.GetIndexes(), x => x.IsUnique &&
            x.Properties.Select(p => p.Name).SequenceEqual(["TripPlanId", "OwnerUserId", "CreateOperationId"]));
        Assert.Contains(memory.GetIndexes(), x => x.GetFilter() == "[DeletedAtUtc] IS NULL");
        Assert.Contains(media.GetIndexes(), x => x.IsUnique && x.Properties.Single().Name == "ObjectKey" &&
            x.GetFilter() == "[ObjectKey] IS NOT NULL");
        Assert.Contains(media.GetIndexes(), x => x.IsUnique && x.Properties.Single().Name == "MemoryId");
        Assert.All(new[] { memory, media }, entity =>
        {
            var version = entity.FindProperty("RowVersion")!;
            Assert.True(version.IsConcurrencyToken);
            Assert.Equal("rowversion", version.GetColumnType());
        });

        Assert.Contains(media.GetCheckConstraints(), x => x.Name == "CK_TripSafety_TripMemoryMedia_State");
        var snapshot = db.GetService<IMigrationsAssembly>().ModelSnapshot!.Model;
        Assert.Contains(snapshot.GetEntityTypes(), t => t.GetTableName() == "TripSafety_TripMemories");
        Assert.Contains(snapshot.GetEntityTypes(), t => t.GetTableName() == "TripSafety_TripMemoryMedia");
        var snapshotStop = snapshot.FindEntityType(typeof(TripStop).FullName!)!;
        Assert.Equal(2, snapshotStop.GetKeys().Count());
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TrackingStopDeletionPreservesActiveAndTombstoneLinks(bool tombstone)
    {
        using var db = new WayhunterSharedDbContext(new DbContextOptionsBuilder<WayhunterSharedDbContext>()
            .UseSqlServer("Server=localhost;Database=MS07ModelOnly;Integrated Security=True").Options);
        var owner = Guid.NewGuid().ToString("D");
        var now = DateTimeOffset.UtcNow;
        var day = DateOnly.FromDateTime(now.UtcDateTime);
        var trip = new TripPlan(owner, "Trip", day, day.AddDays(1), 100m, "Car", 2);
        var stop = trip.AddStop("Stop");
        var memory = new TripMemoryRow
        {
            MemoryId = Guid.NewGuid(), TripPlanId = trip.Id, TripStopId = stop.Id,
            OwnerUserId = owner, CreateOperationId = Guid.NewGuid(), CreatedAtUtc = now,
            UpdatedAtUtc = now, DeletedAtUtc = tombstone ? now : null
        };
        db.Attach(trip);
        db.Attach(memory);

        db.Remove(stop);
        db.ChangeTracker.CascadeChanges();

        Assert.Equal(EntityState.Deleted, db.Entry(stop).State);
        Assert.Equal(EntityState.Unchanged, db.Entry(memory).State);
        Assert.Equal(stop.Id, memory.TripStopId);
    }

}
