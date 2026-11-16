
using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Wayhunter.Domain.Modules.HunterPlaces.Entities;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Wayhunter.Infrastructure.Persistence.Configurations.HunterPlaces;
using Wayhunter.Infrastructure.Persistence.Configurations.IdentitySocial;
using Wayhunter.Infrastructure.Persistence.Models.HunterPlaces;
using Xunit;

namespace Wayhunter.UnitTests.Modules.HunterPlaces;

public sealed class HunterCaptureSessionEfModelTests
{
    // Model-only test context.
    // No SQL connection or migration is executed.
    private sealed class MappingContext : DbContext
    {
        public MappingContext(
            DbContextOptions<MappingContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(
            ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.ApplyConfiguration(
                new UserAccountConfiguration());

            modelBuilder.ApplyConfiguration(
                new HunterSubmissionConfiguration());

            modelBuilder.ApplyConfiguration(
                new HunterEvidenceConfiguration());

            modelBuilder.ApplyConfiguration(
                new HunterReviewDecisionConfiguration());

            modelBuilder.ApplyConfiguration(
                new HunterCaptureSessionRowConfiguration());
        }
    }

    private static MappingContext CreateContext()
    {
        var options =
            new DbContextOptionsBuilder<MappingContext>()
                .UseSqlServer(
                    "Server=localhost;" +
                    "Database=Wayhunter_M4_ModelOnly;" +
                    "Trusted_Connection=True;" +
                    "TrustServerCertificate=True")
                .Options;

        return new MappingContext(options);
    }

    [Fact]
    public void M4_AllEntities_ShouldBeMapped()
    {
        using var context = CreateContext();

        Assert.NotNull(context.Model.FindEntityType(
            typeof(HunterSubmission)));

        Assert.NotNull(context.Model.FindEntityType(
            typeof(HunterEvidence)));

        Assert.NotNull(context.Model.FindEntityType(
            typeof(HunterReviewDecision)));

        Assert.NotNull(context.Model.FindEntityType(
            typeof(HunterCaptureSessionRow)));
    }

    [Fact]
    public void M4_ShouldUseExpectedTableNames()
    {
        using var context = CreateContext();

        Assert.Equal(
            "Hunter_Submissions",
            context.Model.FindEntityType(
                typeof(HunterSubmission))?.GetTableName());

        Assert.Equal(
            "Hunter_Evidence",
            context.Model.FindEntityType(
                typeof(HunterEvidence))?.GetTableName());

        Assert.Equal(
            "Hunter_ReviewDecisions",
            context.Model.FindEntityType(
                typeof(HunterReviewDecision))?.GetTableName());

        Assert.Equal(
            "Hunter_CaptureSessions",
            context.Model.FindEntityType(
                typeof(HunterCaptureSessionRow))?.GetTableName());
    }

    [Fact]
    public void CaptureSession_ShouldHaveGuidPrimaryKey()
    {
        using var context = CreateContext();

        var entity = context.Model.FindEntityType(
            typeof(HunterCaptureSessionRow));

        Assert.NotNull(entity);

        var key = entity.FindPrimaryKey();

        Assert.NotNull(key);

        var property = Assert.Single(key.Properties);

        Assert.Equal(
            nameof(HunterCaptureSessionRow.Id),
            property.Name);

        Assert.Equal(typeof(Guid), property.ClrType);
    }

    [Fact]
    public void CaptureSession_ShouldHaveSubmissionForeignKey()
    {
        using var context = CreateContext();

        var entity = context.Model.FindEntityType(
            typeof(HunterCaptureSessionRow));

        Assert.NotNull(entity);

        var foreignKey =
            Assert.Single(entity.GetForeignKeys());

        Assert.Equal(
            typeof(HunterSubmission),
            foreignKey.PrincipalEntityType.ClrType);

        Assert.Equal(
            nameof(HunterCaptureSessionRow.HunterSubmissionId),
            Assert.Single(foreignKey.Properties).Name);

        Assert.Equal(
            DeleteBehavior.NoAction,
            foreignKey.DeleteBehavior);
    }

    [Fact]
    public void HunterSubmission_ShouldReferenceM1User()
    {
        using var context = CreateContext();

        var entity = context.Model.FindEntityType(
            typeof(HunterSubmission));

        Assert.NotNull(entity);

        Assert.Contains(
            entity.GetForeignKeys(),
            fk =>
                fk.PrincipalEntityType.ClrType ==
                    typeof(UserAccount) &&
                fk.Properties.Any(p =>
                    p.Name == nameof(HunterSubmission.UserId)));
    }

    [Fact]
    public void CaptureSession_ShouldUseDatetimeoffset()
    {
        using var context = CreateContext();

        var entity = context.Model.FindEntityType(
            typeof(HunterCaptureSessionRow));

        Assert.NotNull(entity);

        Assert.Equal(
            "datetimeoffset",
            entity.FindProperty(
                nameof(HunterCaptureSessionRow.IssuedAtUtc))
                ?.GetColumnType());

        Assert.Equal(
            "datetimeoffset",
            entity.FindProperty(
                nameof(HunterCaptureSessionRow.ExpiresAtUtc))
                ?.GetColumnType());
    }

    [Fact]
    public void CaptureSession_ShouldHaveStateIndexes()
    {
        using var context = CreateContext();

        var entity = context.Model.FindEntityType(
            typeof(HunterCaptureSessionRow));

        Assert.NotNull(entity);

        var names = entity.GetIndexes()
            .Select(x => x.GetDatabaseName())
            .ToArray();

        Assert.Contains(
            "IX_Hunter_CaptureSessions_Submission_State",
            names);

        Assert.Contains(
            "IX_Hunter_CaptureSessions_Hunter_State",
            names);
    }
}
