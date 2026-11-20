using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Wayhunter.Infrastructure.Persistence.Models.IdentitySocial;
using Xunit;

namespace Wayhunter.UnitTests.Modules.IdentitySocial;

public sealed class M1M7AdministratorSchemaMetadataTests
{
    private static IEntityType GetRoleEntity()
    {
        var options = new DbContextOptionsBuilder<WayhunterSharedDbContext>()
            .UseSqlServer("Server=localhost;Database=WayhunterMetadataOnly;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;

        using var context = new WayhunterSharedDbContext(options);

        return context.GetService<IDesignTimeModel>().Model.FindEntityType(
            typeof(UserRoleAssignmentRow))!;
    }

    [Fact]
    public void AdminRole_HasApprovedTablePrimaryKeyAndColumns()
    {
        var entity = GetRoleEntity();

        Assert.Equal("UserRoleAssignments", entity.GetTableName());

        var pk = entity.FindPrimaryKey();
        Assert.NotNull(pk);
        Assert.Equal(
            nameof(UserRoleAssignmentRow.AssignmentId),
            Assert.Single(pk.Properties).Name);

        Assert.Equal(
            "uniqueidentifier",
            entity.FindProperty("AssignmentId")!.GetColumnType());

        AssertColumn(entity, "UserId", "nvarchar(256)", 256, false);
        AssertColumn(entity, "Role", "nvarchar(64)", 64, false);
        AssertColumn(entity, "Status", "nvarchar(16)", 16, false);
    }

    [Fact]
    public void AdminRole_UserIdReferencesUsers_WithNoActionDelete()
    {
        var entity = GetRoleEntity();

        var fk = Assert.Single(entity.GetForeignKeys());

        Assert.Equal(
            typeof(UserAccount),
            fk.PrincipalEntityType.ClrType);

        Assert.Equal(
            "UserId",
            Assert.Single(fk.Properties).Name);

        Assert.Equal(DeleteBehavior.NoAction, fk.DeleteBehavior);
    }

    [Fact]
    public void AdminRole_ActiveAssignmentHasUniqueFilteredIndex()
    {
        var entity = GetRoleEntity();

        var index = Assert.Single(
            entity.GetIndexes(),
            i => i.IsUnique &&
                i.Properties.Select(p => p.Name)
                    .SequenceEqual(new[] { "UserId", "Role" }));

        Assert.Equal(
            "[Status] = N'Active'",
            index.GetFilter());
    }

    [Fact]
    public void AdminRole_CheckConstraintsEnforceAllowedValues()
    {
        var entity = GetRoleEntity();

        var constraints = entity.GetCheckConstraints()
            .ToDictionary(c => c.Name!, c => c.Sql!);

        Assert.Contains(
            "CK_UserRoleAssignments_Role",
            constraints.Keys);

        Assert.Contains(
            "CK_UserRoleAssignments_Status",
            constraints.Keys);

        Assert.Contains(
            "CK_UserRoleAssignments_Revocation",
            constraints.Keys);

        Assert.Contains("N'Admin'",
            constraints["CK_UserRoleAssignments_Role"]);

        foreach (var status in new[] {
            "Active", "Revoked", "Inactive"
        })
        {
            Assert.Contains(
                $"N'{status}'",
                constraints["CK_UserRoleAssignments_Status"]);
        }

        Assert.Contains(
            "RevokedAtUtc",
            constraints["CK_UserRoleAssignments_Revocation"]);
    }

    [Fact]
    public void AdminRole_TimestampsAndAuditColumnsAreMapped()
    {
        var entity = GetRoleEntity();

        AssertColumn(
            entity, "AssignedAtUtc", "datetimeoffset", null, false);

        AssertColumn(
            entity, "RevokedAtUtc", "datetimeoffset", null, true);

        AssertColumn(
            entity, "AssignedByUserId", "nvarchar(256)", 256, true);

        AssertColumn(
            entity, "RevokedByUserId", "nvarchar(256)", 256, true);
    }

    [Fact]
    public void AssignmentId_IsBackendGenerated_NotSqlGenerated()
    {
        var entity = GetRoleEntity();

        var property = entity.FindProperty(
            nameof(UserRoleAssignmentRow.AssignmentId));

        Assert.NotNull(property);
        Assert.Equal(typeof(Guid), property.ClrType);
        Assert.False(property.IsNullable);

        // Approved Option B: trusted backend generates the GUID.
        Assert.Equal(ValueGenerated.Never, property.ValueGenerated);

        // SQL Server must not generate or replace AssignmentId.
        Assert.Null(property.GetDefaultValueSql());
        Assert.Null(property.GetComputedColumnSql());
    }

    [Fact]
    public void TrustedFactory_GeneratesNonEmptyDistinctAssignmentIds()
    {
        var userId = Guid.NewGuid().ToString("D");
        var assignedAtUtc = new DateTimeOffset(
            2026, 11, 19, 0, 0, 0, TimeSpan.Zero);

        var first = UserRoleAssignmentRow.CreateTrusted(
            userId,
            assignedAtUtc,
            null);

        var second = UserRoleAssignmentRow.CreateTrusted(
            userId,
            assignedAtUtc,
            null);

        Assert.NotEqual(Guid.Empty, first.AssignmentId);
        Assert.NotEqual(Guid.Empty, second.AssignmentId);
        Assert.NotEqual(first.AssignmentId, second.AssignmentId);

        Assert.Equal(userId, first.UserId);
        Assert.Equal("Admin", first.Role);
        Assert.Equal("Active", first.Status);
        Assert.Equal(assignedAtUtc, first.AssignedAtUtc);
    }

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("invalid-user-id")]
    public void TrustedFactory_RejectsInvalidUserIds(string userId)
    {
        var assignedAtUtc = new DateTimeOffset(
            2026, 11, 19, 0, 0, 0, TimeSpan.Zero);

        Assert.Throws<ArgumentException>(() =>
            UserRoleAssignmentRow.CreateTrusted(
                userId,
                assignedAtUtc,
                null));
    }
    private static void AssertColumn(
        IEntityType entity,
        string name,
        string columnType,
        int? length,
        bool nullable)
    {
        var property = entity.FindProperty(name);

        Assert.NotNull(property);
        Assert.Equal(columnType, property.GetColumnType());
        Assert.Equal(length, property.GetMaxLength());
        Assert.Equal(nullable, property.IsNullable);
    }
}