using Microsoft.EntityFrameworkCore;
using Wayhunter.Domain.Modules.HunterPlaces.Entities;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Wayhunter.Infrastructure.Persistence.Models.HunterPlaces;
using Xunit;

namespace Wayhunter.UnitTests.Modules.HunterPlaces;

public sealed class HunterEvidenceVerificationEfModelTests
{
    [Fact]
    public void EvidenceModelEnforcesVerifiedReceiptAndSessionUniqueness()
    {
        var options = new DbContextOptionsBuilder<WayhunterSharedDbContext>()
            .UseSqlServer("Server=localhost;Database=Wayhunter_Test_ModelOnly;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;
        using var db = new WayhunterSharedDbContext(options);
        var evidence = db.Model.FindEntityType(typeof(HunterEvidence))!;
        Assert.Equal("Hunter_Evidence", evidence.GetTableName());
        Assert.Contains(evidence.GetIndexes(), x => !x.IsUnique && x.GetDatabaseName() == "IX_Hunter_Evidence_Submission_Type");
        Assert.Contains(evidence.GetIndexes(), x => x.IsUnique && x.GetDatabaseName() == "UX_Hunter_Evidence_CaptureSession");
        Assert.Contains(evidence.GetIndexes(), x => x.IsUnique && x.GetDatabaseName() == "UX_Hunter_Evidence_ProviderAsset");
        Assert.Contains(evidence.GetIndexes(), x => x.IsUnique && x.GetDatabaseName() == "UX_Hunter_Evidence_Verified_Submission_Type");
        Assert.Contains(evidence.GetForeignKeys(), x => x.Properties.Any(p => p.Name == nameof(HunterEvidence.OwnerUserId)));
        Assert.Contains(evidence.GetForeignKeys(), x => x.Properties.Any(p => p.Name == nameof(HunterEvidence.CaptureSessionId)));
    }

    [Fact]
    public void IdempotencyModelUsesActorOperationAndHashedKeyUniqueness()
    {
        var options = new DbContextOptionsBuilder<WayhunterSharedDbContext>()
            .UseSqlServer("Server=localhost;Database=Wayhunter_Test_ModelOnly;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;
        using var db = new WayhunterSharedDbContext(options);
        var entity = db.Model.FindEntityType(typeof(HunterRequestIdempotencyRow))!;
        Assert.Equal("Hunter_RequestIdempotency", entity.GetTableName());
        Assert.Contains(entity.GetIndexes(), x => x.IsUnique &&
            x.GetDatabaseName() == "UX_Hunter_RequestIdempotency_Owner_Operation_Key");
    }
}
