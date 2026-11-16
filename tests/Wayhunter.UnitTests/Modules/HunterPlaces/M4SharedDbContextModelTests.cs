
using Microsoft.EntityFrameworkCore;
using Wayhunter.Domain.Modules.HunterPlaces.Entities;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Wayhunter.Infrastructure.Persistence.Models.HunterPlaces;
using Xunit;

namespace Wayhunter.UnitTests.Modules.HunterPlaces;

public sealed class M4SharedDbContextModelTests
{
    private static WayhunterSharedDbContext CreateContext()
    {
        // Metadata-only: no SQL connection is opened.
        var options =
            new DbContextOptionsBuilder<WayhunterSharedDbContext>()
                .UseSqlServer(
                    "Server=localhost;" +
                    "Database=Wayhunter_M4_ModelOnly;" +
                    "Trusted_Connection=True;" +
                    "TrustServerCertificate=True")
                .Options;

        return new WayhunterSharedDbContext(options);
    }

    [Fact]
    public void SharedModel_ShouldContainM4Entities()
    {
        using var db = CreateContext();

        Assert.NotNull(db.Model.FindEntityType(
            typeof(HunterSubmission)));

        Assert.NotNull(db.Model.FindEntityType(
            typeof(HunterEvidence)));

        Assert.NotNull(db.Model.FindEntityType(
            typeof(HunterReviewDecision)));

        Assert.NotNull(db.Model.FindEntityType(
            typeof(HunterCaptureSessionRow)));
    }

    [Fact]
    public void M4_ShouldUseCorrectTableNames()
    {
        using var db = CreateContext();

        Assert.Equal(
            "Hunter_Submissions",
            db.Model.FindEntityType(
                typeof(HunterSubmission))?.GetTableName());

        Assert.Equal(
            "Hunter_Evidence",
            db.Model.FindEntityType(
                typeof(HunterEvidence))?.GetTableName());

        Assert.Equal(
            "Hunter_ReviewDecisions",
            db.Model.FindEntityType(
                typeof(HunterReviewDecision))?.GetTableName());

        Assert.Equal(
            "Hunter_CaptureSessions",
            db.Model.FindEntityType(
                typeof(HunterCaptureSessionRow))?.GetTableName());
    }

    [Fact]
    public void HunterSubmission_ShouldHaveM1ForeignKey()
    {
        using var db = CreateContext();

        var entity = db.Model.FindEntityType(
            typeof(HunterSubmission));

        Assert.NotNull(entity);

        Assert.Contains(
            entity.GetForeignKeys(),
            fk =>
                fk.PrincipalEntityType.ClrType ==
                    typeof(UserAccount) &&
                fk.Properties.Count == 1 &&
                fk.Properties[0].Name ==
                    nameof(HunterSubmission.UserId) &&
                fk.DeleteBehavior ==
                    DeleteBehavior.NoAction);
    }
}
