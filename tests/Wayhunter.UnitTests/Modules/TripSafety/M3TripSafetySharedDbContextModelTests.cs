
using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Wayhunter.Domain.Modules.TravelPlanning.Entities;
using Wayhunter.Domain.Modules.TripSafety.Entities;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Xunit;

namespace Wayhunter.UnitTests.Modules.TripSafety;

public sealed class M3TripSafetySharedDbContextModelTests
{
    // SQL Server provider model metadata only.
    // No connection is opened.
    // No migration or database update is executed.

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

    private static IEntityType Entity(
        WayhunterSharedDbContext context,
        Type entityType)
    {
        return context.Model.FindEntityType(entityType)
            ?? throw new InvalidOperationException(
                $"Entity is not mapped: {entityType.Name}");
    }

    // Check constraints are inspected from the design-time model,
    // not the read-optimized runtime model.
    private static IEntityType DesignEntity(
        WayhunterSharedDbContext context,
        Type entityType)
    {
        var designModel =
            context.GetService<IDesignTimeModel>().Model;

        return designModel.FindEntityType(entityType)
            ?? throw new InvalidOperationException(
                $"Design-time entity is not mapped: {entityType.Name}");
    }

    private static IForeignKey ForeignKey(
        IEntityType entity,
        Type principalType,
        string dependentProperty)
    {
        return Assert.Single(
            entity.GetForeignKeys(),
            fk =>
                fk.PrincipalEntityType.ClrType == principalType &&
                fk.Properties.Any(
                    p => p.Name == dependentProperty));
    }

    private static bool HasUniqueIndex(
        IEntityType entity,
        string[] properties,
        string? filter = null)
    {
        return entity.GetIndexes().Any(index =>
            index.IsUnique &&
            index.Properties.Select(x => x.Name)
                .SequenceEqual(properties) &&
            index.GetFilter() == filter);
    }

    private static void AssertNoAction(
        IForeignKey foreignKey)
    {
        Assert.Equal(
            DeleteBehavior.NoAction,
            foreignKey.DeleteBehavior);
    }

    // ==========================================
    // TABLE MAPPINGS
    // ==========================================

    [Theory]
    [InlineData(typeof(TripInvitation), "TripSafety_TripInvitations")]
    [InlineData(typeof(TripMember), "TripSafety_TripMembers")]
    [InlineData(typeof(TripGpsConsent), "TripSafety_GpsConsents")]
    [InlineData(typeof(HazardReport), "TripSafety_HazardReports")]
    public void M3_Entities_Should_Map_Correct_Table(
        Type entityType,
        string expectedTable)
    {
        using var context = CreateContext();

        Assert.Equal(
            expectedTable,
            Entity(context, entityType).GetTableName());
    }

    // ==========================================
    // PRIMARY KEYS
    // ==========================================

    [Theory]
    [InlineData(typeof(TripInvitation), nameof(TripInvitation.Id))]
    [InlineData(typeof(TripMember), nameof(TripMember.Id))]
    [InlineData(typeof(HazardReport), nameof(HazardReport.Id))]
    public void M3_Entities_Should_Have_Guid_PrimaryKey(
        Type entityType,
        string expectedProperty)
    {
        using var context = CreateContext();

        var entity = Entity(context, entityType);
        var primaryKey = entity.FindPrimaryKey();

        Assert.NotNull(primaryKey);

        var property = Assert.Single(primaryKey.Properties);

        Assert.Equal(expectedProperty, property.Name);
        Assert.Equal(typeof(Guid), property.ClrType);
    }

    [Fact]
    public void GpsConsent_Should_Have_Composite_PrimaryKey()
    {
        using var context = CreateContext();

        var entity = Entity(context, typeof(TripGpsConsent));
        var primaryKey = entity.FindPrimaryKey();

        Assert.NotNull(primaryKey);

        Assert.Collection(
            primaryKey.Properties,
            property =>
            {
                Assert.Equal(
                    nameof(TripGpsConsent.SessionId),
                    property.Name);

                Assert.Equal(typeof(Guid), property.ClrType);
            },
            property =>
            {
                Assert.Equal(
                    nameof(TripGpsConsent.UserId),
                    property.Name);

                Assert.Equal(typeof(string), property.ClrType);
            });
    }

    // ==========================================
    // M1 USER ID CONTRACT
    // ==========================================

    [Fact]
    public void M3_UserReferences_Should_Use_M1_UserId_Length()
    {
        using var context = CreateContext();

        var invitation = Entity(context, typeof(TripInvitation));
        var member = Entity(context, typeof(TripMember));
        var consent = Entity(context, typeof(TripGpsConsent));
        var hazard = Entity(context, typeof(HazardReport));

        Assert.Equal(
            256,
            invitation.FindProperty(
                nameof(TripInvitation.SenderUserId))!.GetMaxLength());

        Assert.Equal(
            256,
            invitation.FindProperty(
                nameof(TripInvitation.RecipientUserId))!.GetMaxLength());

        Assert.Equal(
            256,
            member.FindProperty(
                nameof(TripMember.MemberUserId))!.GetMaxLength());

        Assert.Equal(
            256,
            consent.FindProperty(
                nameof(TripGpsConsent.UserId))!.GetMaxLength());

        Assert.Equal(
            256,
            hazard.FindProperty(
                nameof(HazardReport.ReporterUserId))!.GetMaxLength());
    }

    // ==========================================
    // TRIP INVITATION FOREIGN KEYS
    // ==========================================

    [Fact]
    public void TripInvitation_Should_Have_TripPlan_ForeignKey()
    {
        using var context = CreateContext();

        var entity = Entity(context, typeof(TripInvitation));

        var fk = ForeignKey(
            entity,
            typeof(TripPlan),
            nameof(TripInvitation.TripPlanId));

        Assert.Equal(
            nameof(TripPlan.Id),
            Assert.Single(fk.PrincipalKey.Properties).Name);

        Assert.True(fk.IsRequired);
        AssertNoAction(fk);
    }

    [Fact]
    public void TripInvitation_Should_Have_Two_User_ForeignKeys()
    {
        using var context = CreateContext();

        var entity = Entity(context, typeof(TripInvitation));

        var sender = ForeignKey(
            entity,
            typeof(UserAccount),
            nameof(TripInvitation.SenderUserId));

        var recipient = ForeignKey(
            entity,
            typeof(UserAccount),
            nameof(TripInvitation.RecipientUserId));

        Assert.True(sender.IsRequired);
        Assert.True(recipient.IsRequired);

        Assert.Equal(
            nameof(UserAccount.UserId),
            Assert.Single(sender.PrincipalKey.Properties).Name);

        Assert.Equal(
            nameof(UserAccount.UserId),
            Assert.Single(recipient.PrincipalKey.Properties).Name);

        AssertNoAction(sender);
        AssertNoAction(recipient);
    }

    // ==========================================
    // TRIP MEMBER FOREIGN KEYS
    // ==========================================

    [Fact]
    public void TripMember_Should_Have_Trip_User_And_Invitation_FKs()
    {
        using var context = CreateContext();

        var entity = Entity(context, typeof(TripMember));

        var tripFk = ForeignKey(
            entity,
            typeof(TripPlan),
            nameof(TripMember.TripPlanId));

        var userFk = ForeignKey(
            entity,
            typeof(UserAccount),
            nameof(TripMember.MemberUserId));

        var invitationFk = ForeignKey(
            entity,
            typeof(TripInvitation),
            nameof(TripMember.InvitationId));

        Assert.True(tripFk.IsRequired);
        Assert.True(userFk.IsRequired);
        Assert.True(invitationFk.IsRequired);

        AssertNoAction(tripFk);
        AssertNoAction(userFk);
        AssertNoAction(invitationFk);
    }

    // ==========================================
    // GPS CONSENT FOREIGN KEYS
    // ==========================================

    [Fact]
    public void TripGpsConsent_Should_Have_Session_And_User_FKs()
    {
        using var context = CreateContext();

        var entity = Entity(context, typeof(TripGpsConsent));

        var sessionFk = ForeignKey(
            entity,
            typeof(LiveTripSession),
            nameof(TripGpsConsent.SessionId));

        var userFk = ForeignKey(
            entity,
            typeof(UserAccount),
            nameof(TripGpsConsent.UserId));

        Assert.True(sessionFk.IsRequired);
        Assert.True(userFk.IsRequired);

        AssertNoAction(sessionFk);
        AssertNoAction(userFk);
    }

    // ==========================================
    // HAZARD FOREIGN KEYS
    // ==========================================

    [Fact]
    public void HazardReport_Should_Have_Three_User_FKs()
    {
        using var context = CreateContext();

        var entity = Entity(context, typeof(HazardReport));

        var reporter = ForeignKey(
            entity,
            typeof(UserAccount),
            nameof(HazardReport.ReporterUserId));

        var verifier = ForeignKey(
            entity,
            typeof(UserAccount),
            nameof(HazardReport.VerifiedByUserId));

        var resolver = ForeignKey(
            entity,
            typeof(UserAccount),
            nameof(HazardReport.ResolvedByUserId));

        Assert.True(reporter.IsRequired);
        Assert.False(verifier.IsRequired);
        Assert.False(resolver.IsRequired);

        AssertNoAction(reporter);
        AssertNoAction(verifier);
        AssertNoAction(resolver);
    }

    // ==========================================
    // INDEXES
    // ==========================================

    [Fact]
    public void TripInvitation_Should_Have_OpenRecipient_UniqueIndex()
    {
        using var context = CreateContext();

        var entity = Entity(context, typeof(TripInvitation));

        Assert.True(HasUniqueIndex(
            entity,
            new[]
            {
                nameof(TripInvitation.TripPlanId),
                nameof(TripInvitation.RecipientUserId)
            },
            "[Status] IN (0, 1)"));
    }

    [Fact]
    public void TripMember_Should_Have_Unique_Invitation_Index()
    {
        using var context = CreateContext();

        var entity = Entity(context, typeof(TripMember));

        Assert.True(HasUniqueIndex(
            entity,
            new[]
            {
                nameof(TripMember.InvitationId)
            }));
    }

    [Fact]
    public void TripMember_Should_Have_ActiveMember_FilteredIndex()
    {
        using var context = CreateContext();

        var entity = Entity(context, typeof(TripMember));

        Assert.True(HasUniqueIndex(
            entity,
            new[]
            {
                nameof(TripMember.TripPlanId),
                nameof(TripMember.MemberUserId)
            },
            "[IsActive] = 1"));
    }

    [Fact]
    public void HazardReport_Should_Have_Expiry_And_Reporter_Indexes()
    {
        using var context = CreateContext();

        var entity = Entity(context, typeof(HazardReport));

        Assert.Contains(
            entity.GetIndexes(),
            index =>
                index.Properties.Select(x => x.Name)
                    .SequenceEqual(new[]
                    {
                        nameof(HazardReport.Status),
                        nameof(HazardReport.ExpiresAtUtc)
                    }));

        Assert.Contains(
            entity.GetIndexes(),
            index =>
                index.Properties.Select(x => x.Name)
                    .SequenceEqual(new[]
                    {
                        nameof(HazardReport.ReporterUserId),
                        nameof(HazardReport.ReportedAtUtc)
                    }));
    }

    // ==========================================
    // CHECK CONSTRAINTS
    // ==========================================

    [Theory]
    [InlineData(typeof(TripInvitation),
        "CK_TripSafety_TripInvitations_Status")]
    [InlineData(typeof(TripInvitation),
        "CK_TripSafety_TripInvitations_ResponseTime")]
    [InlineData(typeof(TripMember),
        "CK_TripSafety_TripMembers_RemovalState")]
    [InlineData(typeof(TripGpsConsent),
        "CK_TripSafety_GpsConsents_GrantState")]
    [InlineData(typeof(TripGpsConsent),
        "CK_TripSafety_GpsConsents_RevocationTime")]
    [InlineData(typeof(HazardReport),
        "CK_TripSafety_HazardReports_Status")]
    [InlineData(typeof(HazardReport),
        "CK_TripSafety_HazardReports_Expiry")]
    [InlineData(typeof(HazardReport),
        "CK_TripSafety_HazardReports_Verification")]
    [InlineData(typeof(HazardReport),
        "CK_TripSafety_HazardReports_Resolution")]
    [InlineData(typeof(HazardReport),
        "CK_TripSafety_HazardReports_ExpiryTimestamp")]
    [InlineData(typeof(HazardReport),
        "CK_TripSafety_HazardReports_VerifiedStatus")]
    [InlineData(typeof(HazardReport),
        "CK_TripSafety_HazardReports_ResolvedStatus")]
    [InlineData(typeof(HazardReport),
        "CK_TripSafety_HazardReports_ExpiredStatus")]
    public void M3_Should_Contain_Expected_CheckConstraint(
        Type entityType,
        string constraintName)
    {
        using var context = CreateContext();

        var entity = DesignEntity(context, entityType);

        Assert.Contains(
            entity.GetCheckConstraints(),
            constraint => constraint.Name == constraintName);
    }

    // ==========================================
    // SQL DATA TYPE VERIFICATION
    // ==========================================

    [Fact]
    public void M3_TripPlan_And_Session_Ids_Should_Use_UniqueIdentifier()
    {
        using var context = CreateContext();

        var invitation = Entity(context, typeof(TripInvitation));
        var member = Entity(context, typeof(TripMember));
        var session = Entity(context, typeof(LiveTripSession));

        Assert.Equal(
            "uniqueidentifier",
            invitation.FindProperty(
                nameof(TripInvitation.TripPlanId))!.GetColumnType());

        Assert.Equal(
            "uniqueidentifier",
            member.FindProperty(
                nameof(TripMember.TripPlanId))!.GetColumnType());

        Assert.Equal(
            "uniqueidentifier",
            session.FindProperty(
                nameof(LiveTripSession.TripPlanId))!.GetColumnType());
    }

    [Fact]
    public void M3_Timestamps_Should_Use_DateTimeOffset()
    {
        using var context = CreateContext();

        var invitation = Entity(context, typeof(TripInvitation));
        var member = Entity(context, typeof(TripMember));
        var consent = Entity(context, typeof(TripGpsConsent));
        var hazard = Entity(context, typeof(HazardReport));

        Assert.Equal(
            "datetimeoffset",
            invitation.FindProperty(
                nameof(TripInvitation.CreatedAtUtc))!.GetColumnType());

        Assert.Equal(
            "datetimeoffset",
            member.FindProperty(
                nameof(TripMember.JoinedAtUtc))!.GetColumnType());

        Assert.Equal(
            "datetimeoffset",
            consent.FindProperty(
                nameof(TripGpsConsent.GrantedAtUtc))!.GetColumnType());

        Assert.Equal(
            "datetimeoffset",
            hazard.FindProperty(
                nameof(HazardReport.ReportedAtUtc))!.GetColumnType());

        Assert.Equal(
            "datetimeoffset",
            hazard.FindProperty(
                nameof(HazardReport.ExpiresAtUtc))!.GetColumnType());
    }

    // ==========================================
    // ENUM CONVERSION
    // ==========================================

    [Theory]
    [InlineData(typeof(TripInvitation), nameof(TripInvitation.Status))]
    [InlineData(typeof(HazardReport), nameof(HazardReport.Status))]
    public void Lifecycle_Status_Should_Use_Integer_Conversion(
        Type entityType,
        string propertyName)
    {
        using var context = CreateContext();

        var entity = Entity(context, entityType);
        var property = entity.FindProperty(propertyName);

        Assert.NotNull(property);

        // EF Core may keep the converter on the relational
        // type mapping rather than IProperty.GetValueConverter().
        var providerType =
            property.GetRelationalTypeMapping()
                .Converter?.ProviderClrType
            ?? property.GetProviderClrType();

        Assert.Equal(typeof(int), providerType);
    }

    // ==========================================
    // SHARED CONTEXT REGISTRATION
    // ==========================================

    [Fact]
    public void SharedContext_Should_Register_All_M3_Entities()
    {
        using var context = CreateContext();

        Assert.NotNull(context.Model.FindEntityType(
            typeof(LiveTripSession)));

        Assert.NotNull(context.Model.FindEntityType(
            typeof(TripInvitation)));

        Assert.NotNull(context.Model.FindEntityType(
            typeof(TripMember)));

        Assert.NotNull(context.Model.FindEntityType(
            typeof(TripGpsConsent)));

        Assert.NotNull(context.Model.FindEntityType(
            typeof(HazardReport)));
    }

    [Fact]
    public void SharedContext_Should_Not_Have_Duplicate_Table_Names()
    {
        using var context = CreateContext();

        var names = context.Model.GetEntityTypes()
            .Select(x => x.GetTableName())
            .Where(x => x is not null)
            .ToArray();

        Assert.Equal(
            names.Length,
            names.Distinct(
                StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void GeneratedSql_Should_Contain_M3_Tables_And_Constraints()
    {
        using var context = CreateContext();

        string sql = context.Database.GenerateCreateScript();

        string[] tables =
        {
            "TripSafety_TripInvitations",
            "TripSafety_TripMembers",
            "TripSafety_GpsConsents",
            "TripSafety_HazardReports"
        };

        string[] constraints =
        {
            "CK_TripSafety_TripInvitations_Status",
            "CK_TripSafety_TripMembers_RemovalState",
            "CK_TripSafety_GpsConsents_GrantState",
            "CK_TripSafety_HazardReports_Expiry",
            "CK_TripSafety_HazardReports_VerifiedStatus",
            "CK_TripSafety_HazardReports_ResolvedStatus",
            "CK_TripSafety_HazardReports_ExpiredStatus",
            "FK_TripSafety_TripInvitations_Trips_TripPlanId",
            "FK_TripSafety_TripMembers_TripInvitations_InvitationId"
        };

        foreach (var table in tables)
        {
            Assert.Contains(
                $"CREATE TABLE [{table}]",
                sql);
        }

        foreach (var constraint in constraints)
        {
            Assert.Contains(constraint, sql);
        }
    }
}
