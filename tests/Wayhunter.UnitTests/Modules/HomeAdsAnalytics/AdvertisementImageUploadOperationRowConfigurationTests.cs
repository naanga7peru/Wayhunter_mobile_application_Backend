using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Wayhunter.Infrastructure.Persistence.Configurations.HomeAdsAnalytics;
using Wayhunter.Infrastructure.Persistence.Models.HomeAdsAnalytics;
using Xunit;

namespace Wayhunter.UnitTests.Modules.HomeAdsAnalytics;

public sealed class AdvertisementImageUploadOperationRowConfigurationTests
{
    private static ProposedContext CreateContext()
    {
        // Model inspection only. No SQL connection is opened.
        var options = new DbContextOptionsBuilder<ProposedContext>()
            .UseSqlServer(
                "Server=localhost;Database=WayhunterModelOnly;" +
                "Trusted_Connection=True;TrustServerCertificate=True")
            .Options;

        return new ProposedContext(options);
    }

    [Fact]
    public void OperationMapping_HasPrimaryKeyAndExpectedTable()
    {
        using var db = CreateContext();

        var entity = db.Model.FindEntityType(
            typeof(AdvertisementImageUploadOperationRow));

        Assert.NotNull(entity);

        Assert.Equal(
            "AdvertisementImageUploadOperations",
            entity.GetTableName());

        var pk = entity.FindPrimaryKey();

        Assert.NotNull(pk);
        Assert.Equal(
            nameof(AdvertisementImageUploadOperationRow.UploadOperationId),
            Assert.Single(pk.Properties).Name);
    }

    [Fact]
    public void OperationMapping_HasRowVersionConcurrencyToken()
    {
        using var db = CreateContext();

        var entity = db.Model.FindEntityType(
            typeof(AdvertisementImageUploadOperationRow));

        Assert.NotNull(entity);

        var token = entity.FindProperty(
            nameof(AdvertisementImageUploadOperationRow.RowVersion));

        Assert.NotNull(token);
        Assert.True(token.IsConcurrencyToken);
        Assert.Equal(
            ValueGenerated.OnAddOrUpdate,
            token.ValueGenerated);
    }

    [Fact]
    public void OperationMapping_HasRecoveryIndexes()
    {
        using var db = CreateContext();

        var entity = db.Model.FindEntityType(
            typeof(AdvertisementImageUploadOperationRow));

        Assert.NotNull(entity);

        var names = entity.GetIndexes()
            .Select(index => index.GetDatabaseName())
            .ToHashSet();

        Assert.Contains("IX_AdImageOps_Ad_State", names);
        Assert.Contains("IX_AdImageOps_State_Retry", names);
        Assert.Contains("IX_AdImageOps_Claim_Lease", names);
        Assert.Contains("IX_AdImageOps_Provider_Asset", names);
    }

    [Fact]
    public void OperationMapping_KeepsApprovalDependentFkUnconfigured()
    {
        using var db = CreateContext();

        // CHECK constraints require the EF design-time model.
        var entity = db.GetService<IDesignTimeModel>()
            .Model.FindEntityType(
                typeof(AdvertisementImageUploadOperationRow));

        Assert.NotNull(entity);

        Assert.Empty(entity.GetForeignKeys());

        var constraints = entity.GetCheckConstraints()
            .Select(check => check.Name)
            .ToHashSet();

        Assert.Contains("CK_AdImageOps_State", constraints);
        Assert.Contains("CK_AdImageOps_NonNegative", constraints);
        Assert.Contains("CK_AdImageOps_LeasePair", constraints);
    }

    private sealed class ProposedContext : DbContext
    {
        public ProposedContext(
            DbContextOptions<ProposedContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(
            ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfiguration(
                new AdvertisementImageUploadOperationRowConfiguration());
        }
    }
}