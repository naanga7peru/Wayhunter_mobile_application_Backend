using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Wayhunter.Infrastructure.Persistence.Configurations.HomeAdsAnalytics;
using Wayhunter.Infrastructure.Persistence.Models.HomeAdsAnalytics;
using Xunit;

namespace Wayhunter.UnitTests.Modules.HomeAdsAnalytics;

public sealed class AdvertisementImageUploadAuditEventRowTests
{
    private static AuditModelContext CreateContext()
    {
        // EF model inspection only. No SQL connection is opened.
        var options = new DbContextOptionsBuilder<AuditModelContext>()
            .UseSqlServer(
                "Server=localhost;Database=WayhunterAuditModelOnly;" +
                "Integrated Security=True;TrustServerCertificate=True")
            .Options;

        return new AuditModelContext(options);
    }

    [Fact]
    public void AuditMapping_HasExpectedTableAndPrimaryKey()
    {
        using var db = CreateContext();

        var entity = db.Model.FindEntityType(
            typeof(AdvertisementImageUploadAuditEventRow));

        Assert.NotNull(entity);

        Assert.Equal(
            "AdvertisementImageUploadAuditEvents",
            entity.GetTableName());

        var primaryKey = entity.FindPrimaryKey();

        Assert.NotNull(primaryKey);
        Assert.Equal("EventId", Assert.Single(primaryKey.Properties).Name);
    }

    [Fact]
    public void AuditMapping_HasOperationAndCorrelationIndexes()
    {
        using var db = CreateContext();

        var entity = db.Model.FindEntityType(
            typeof(AdvertisementImageUploadAuditEventRow));

        Assert.NotNull(entity);

        var indexes = entity.GetIndexes()
            .Select(x => x.GetDatabaseName())
            .ToHashSet();

        Assert.Contains(
            "IX_AdImageAudit_Operation_Time", indexes);

        Assert.Contains(
            "IX_AdImageAudit_Correlation_Time", indexes);
    }

    [Fact]
    public void AuditMapping_LimitsActorAndErrorFields()
    {
        using var db = CreateContext();

        var entity = db.Model.FindEntityType(
            typeof(AdvertisementImageUploadAuditEventRow));

        Assert.NotNull(entity);

        Assert.Equal(64,
            entity.FindProperty("EventType")!.GetMaxLength());

        Assert.Equal(32,
            entity.FindProperty("ActorKind")!.GetMaxLength());

        Assert.Equal(128,
            entity.FindProperty("ActorId")!.GetMaxLength());

        Assert.Equal(128,
            entity.FindProperty("ErrorCode")!.GetMaxLength());

        Assert.Equal(128,
            entity.FindProperty("CorrelationId")!.GetMaxLength());
    }

    [Fact]
    public void AuditMapping_HasStateCheckConstraints()
    {
        using var db = CreateContext();

        var entity = db.GetService<IDesignTimeModel>()
            .Model.FindEntityType(
                typeof(AdvertisementImageUploadAuditEventRow));

        Assert.NotNull(entity);

        var checks = entity.GetCheckConstraints()
            .Select(x => x.Name)
            .ToHashSet();

        Assert.Contains("CK_AdImageAudit_PreviousState", checks);
        Assert.Contains("CK_AdImageAudit_NewState", checks);
        Assert.Contains("CK_AdImageAudit_EventType", checks);
    }

    [Fact]
    public void AuditMapping_DoesNotConfigureUnapprovedForeignKeys()
    {
        using var db = CreateContext();

        var entity = db.Model.FindEntityType(
            typeof(AdvertisementImageUploadAuditEventRow));

        Assert.NotNull(entity);
        Assert.Empty(entity.GetForeignKeys());
    }

    private sealed class AuditModelContext : DbContext
    {
        public AuditModelContext(
            DbContextOptions<AuditModelContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(
            ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfiguration(
                new AdvertisementImageUploadAuditEventRowConfiguration());
        }
    }
}