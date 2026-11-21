using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Wayhunter.Domain.Modules.TravelPlanning.Entities;
using Wayhunter.Domain.Modules.TripSafety.Entities;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Wayhunter.Infrastructure.Persistence.Models.TripSafety;
using Xunit;

namespace Wayhunter.UnitTests.Modules.TripSafety;

public sealed class M3SafeCheckInSchemaMetadataTests
{
    private static WayhunterSharedDbContext CreateContext() => new(
        new DbContextOptionsBuilder<WayhunterSharedDbContext>()
            .UseSqlServer("Server=localhost;Database=Wayhunter_M3_ModelOnly;Integrated Security=True")
            .Options);

    [Fact]
    public void CheckIn_HasExactColumns_WithNoLocationData()
    {
        using var context = CreateContext();
        var entity = context.Model.FindEntityType(typeof(TripSafeCheckInRow))!;

        Assert.Equal("TripSafety_SafeCheckIns", entity.GetTableName());
        AssertColumns(entity,
            ("CheckInId", "uniqueidentifier", false),
            ("TripPlanId", "uniqueidentifier", false),
            ("SessionId", "uniqueidentifier", false),
            ("UserId", "nvarchar(256)", false),
            ("CheckedInAtUtc", "datetimeoffset(7)", false));
        AssertPrimaryKey(entity, "CheckInId");

        var userKey = context.Model.FindEntityType(typeof(UserAccount))!.FindPrimaryKey()!.Properties.Single();
        var checkInUser = entity.FindProperty("UserId")!;
        Assert.Equal(typeof(string), checkInUser.ClrType);
        Assert.Equal(256, checkInUser.GetMaxLength());
        Assert.Equal(userKey.GetMaxLength(), checkInUser.GetMaxLength());
        Assert.Equal(userKey.GetColumnType(), checkInUser.GetColumnType());
    }

    [Fact]
    public void CheckIn_EnforcesUniqueSessionUserAndExactTripSessionPair()
    {
        using var context = CreateContext();
        var entity = context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(TripSafeCheckInRow))!;
        Assert.Equal(3, entity.GetForeignKeys().Count());
        AssertForeignKey<TripPlan>(entity, ["TripPlanId"], ["Id"]);
        AssertForeignKey<UserAccount>(entity, ["UserId"], ["UserId"]);
        AssertForeignKey<LiveTripSession>(entity, ["SessionId", "TripPlanId"], ["SessionId", "TripPlanId"]);
        Assert.Equal(4, entity.GetIndexes().Count());
        AssertIndex(entity, "UX_TripSafety_SafeCheckIns_SessionId_UserId", true, null, "SessionId", "UserId");
        AssertIndex(entity, "IX_TripSafety_SafeCheckIns_SessionId_TripPlanId", false, null, "SessionId", "TripPlanId");
        AssertIndex(entity, "IX_TripSafety_SafeCheckIns_TripPlanId", false, null, "TripPlanId");
        AssertIndex(entity, "IX_TripSafety_SafeCheckIns_UserId", false, null, "UserId");
    }

    [Fact]
    public void OwnerOutbox_HasExactColumnsAndOneEventPerCheckIn()
    {
        using var context = CreateContext();
        var entity = context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(TripSafeCheckInOutboxRow))!;
        Assert.Equal("TripSafety_SafeCheckInOutbox", entity.GetTableName());
        AssertColumns(entity,
            ("Id", "uniqueidentifier", false),
            ("CheckInId", "uniqueidentifier", false),
            ("RecipientUserId", "nvarchar(256)", false),
            ("CreatedAtUtc", "datetimeoffset(7)", false),
            ("DeliveryAttempts", "int", false),
            ("IsDelivered", "bit", false),
            ("DeliveredAtUtc", "datetimeoffset(7)", true),
            ("NextAttemptAtUtc", "datetimeoffset(7)", true),
            ("LeaseToken", "uniqueidentifier", true),
            ("LeaseUntilUtc", "datetimeoffset(7)", true),
            ("DeadLetteredAtUtc", "datetimeoffset(7)", true),
            ("LastError", "nvarchar(256)", true));

        AssertPrimaryKey(entity, "Id");
        Assert.Equal(0, entity.FindProperty("DeliveryAttempts")!.GetDefaultValue());
        Assert.Equal(false, entity.FindProperty("IsDelivered")!.GetDefaultValue());
        Assert.Equal(256, entity.FindProperty("RecipientUserId")!.GetMaxLength());
        Assert.Equal(256, entity.FindProperty("LastError")!.GetMaxLength());

        Assert.Equal(2, entity.GetForeignKeys().Count());
        Assert.True(AssertForeignKey<TripSafeCheckInRow>(entity, ["CheckInId"], ["CheckInId"]).IsUnique);
        AssertForeignKey<UserAccount>(entity, ["RecipientUserId"], ["UserId"]);
        AssertIndex(entity, "UX_TripSafety_SafeCheckInOutbox_CheckInId", true, null, "CheckInId");
        AssertIndex(entity, "IX_TripSafety_SafeCheckInOutbox_RecipientUserId", false, null, "RecipientUserId");
    }

    [Fact]
    public void OwnerOutbox_HasPendingClaimIndexesAndStateConstraints()
    {
        using var context = CreateContext();
        var entity = context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(TripSafeCheckInOutboxRow))!;
        const string pending = "[IsDelivered] = 0 AND [DeadLetteredAtUtc] IS NULL";
        Assert.Equal(4, entity.GetIndexes().Count());
        AssertIndex(entity, "IX_TripSafety_SafeCheckInOutbox_PendingDue", false, pending,
            "NextAttemptAtUtc", "CreatedAtUtc", "Id");
        AssertIndex(entity, "IX_TripSafety_SafeCheckInOutbox_PendingLeaseExpiry", false,
            pending + " AND [LeaseUntilUtc] IS NOT NULL", "LeaseUntilUtc", "Id");

        var checks = entity.GetCheckConstraints().ToDictionary(x => x.Name!, x => x.Sql);
        Assert.Equal(4, checks.Count);
        Assert.Equal("[DeliveryAttempts] >= 0", checks["CK_TripSafety_SafeCheckInOutbox_Attempts"]);
        Assert.Equal("([IsDelivered] = 0 AND [DeliveredAtUtc] IS NULL) OR " +
            "([IsDelivered] = 1 AND [DeliveredAtUtc] IS NOT NULL AND [DeadLetteredAtUtc] IS NULL)",
            checks["CK_TripSafety_SafeCheckInOutbox_DeliveryState"]);
        Assert.Equal("([LeaseToken] IS NULL AND [LeaseUntilUtc] IS NULL) OR " +
            "([LeaseToken] IS NOT NULL AND [LeaseUntilUtc] IS NOT NULL)",
            checks["CK_TripSafety_SafeCheckInOutbox_LeasePair"]);
        Assert.Equal("([IsDelivered] = 0 AND [DeadLetteredAtUtc] IS NULL) OR " +
            "([LeaseToken] IS NULL AND [LeaseUntilUtc] IS NULL AND [NextAttemptAtUtc] IS NULL)",
            checks["CK_TripSafety_SafeCheckInOutbox_TerminalState"]);
    }

    [Fact]
    public void ProposedTablesAndSessionAlternateKey_AreAbsentFromMigrationSnapshot()
    {
        using var context = CreateContext();
        var migrations = context.GetService<IMigrationsAssembly>();
        Assert.Contains("20261117093023_InitialSharedSchema", migrations.Migrations.Keys);
        var snapshot = migrations.ModelSnapshot!.Model;
        var tables = snapshot.GetEntityTypes().Select(x => x.GetTableName()).ToArray();
        Assert.DoesNotContain("TripSafety_SafeCheckIns", tables);
        Assert.DoesNotContain("TripSafety_SafeCheckInOutbox", tables);
        var session = snapshot.FindEntityType(typeof(LiveTripSession).FullName!)!;
        Assert.Single(session.GetKeys());
    }

    private static void AssertColumns(IEntityType entity, params (string Name, string Type, bool Nullable)[] expected)
    {
        var actual = entity.GetProperties().Select(p => (p.Name, p.GetColumnType(), p.IsNullable));
        Assert.Equal(expected.OrderBy(p => p.Name).ToArray(), actual.OrderBy(p => p.Name).ToArray());
    }

    private static void AssertPrimaryKey(IEntityType entity, string name)
    {
        var property = Assert.Single(entity.FindPrimaryKey()!.Properties);
        Assert.Equal(name, property.Name);
        Assert.Equal(typeof(Guid), property.ClrType);
        Assert.Equal(ValueGenerated.Never, property.ValueGenerated);
    }

    private static IForeignKey AssertForeignKey<T>(IEntityType entity, string[] columns, string[] principalColumns)
    {
        var key = Assert.Single(entity.GetForeignKeys(), x => x.PrincipalEntityType.ClrType == typeof(T));
        Assert.Equal(columns, key.Properties.Select(p => p.Name));
        Assert.Equal(principalColumns, key.PrincipalKey.Properties.Select(p => p.Name));
        Assert.True(key.IsRequired);
        Assert.Equal(DeleteBehavior.NoAction, key.DeleteBehavior);
        return key;
    }

    private static void AssertIndex(IEntityType entity, string name, bool unique, string? filter, params string[] columns)
    {
        var index = Assert.Single(entity.GetIndexes(), x => x.GetDatabaseName() == name);
        Assert.Equal(unique, index.IsUnique);
        Assert.Equal(filter, index.GetFilter());
        Assert.Equal(columns, index.Properties.Select(p => p.Name));
    }
}
