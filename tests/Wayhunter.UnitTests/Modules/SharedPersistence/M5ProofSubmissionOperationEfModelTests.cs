using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Wayhunter.Infrastructure.Persistence.Configurations.Games;
using Wayhunter.Infrastructure.Persistence.Models.Games;
using Xunit;

namespace Wayhunter.UnitTests.Modules.SharedPersistence;

public sealed class M5ProofSubmissionOperationEfModelTests
{
    private sealed class ModelOnlyContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder options)
            => options.UseSqlServer("Server=localhost;Database=Wayhunter_ModelOnly;Trusted_Connection=True;TrustServerCertificate=True");

        protected override void OnModelCreating(ModelBuilder model)
            => model.ApplyConfiguration(new GameProofSubmissionOperationRowConfiguration());
    }

    [Fact]
    public void Operation_HasExpectedTableAndPrimaryKey()
    {
        using var db = new ModelOnlyContext();
        var entity = db.Model.FindEntityType(typeof(GameProofSubmissionOperationRow));
        Assert.NotNull(entity);
        Assert.Equal("GameProofSubmissionOperations", entity.GetTableName());
        Assert.Equal(nameof(GameProofSubmissionOperationRow.SubmissionOperationId), entity.FindPrimaryKey()!.Properties.Single().Name);
    }

    [Fact]
    public void ProviderAndMedia_HaveUniqueIndex()
    {
        using var db = new ModelOnlyContext();
        var entity = db.Model.FindEntityType(typeof(GameProofSubmissionOperationRow))!;
        Assert.Contains(entity.GetIndexes(), index => index.IsUnique && index.Properties.Select(p => p.Name).SequenceEqual(new[] { nameof(GameProofSubmissionOperationRow.MediaProviderNamespace), nameof(GameProofSubmissionOperationRow.CanonicalMediaId) }));
    }

    [Fact]
    public void Reconciliation_HasLookupIndex()
    {
        using var db = new ModelOnlyContext();
        var entity = db.Model.FindEntityType(typeof(GameProofSubmissionOperationRow))!;
        Assert.Contains(entity.GetIndexes(), index => index.Properties.Select(p => p.Name).SequenceEqual(new[] { nameof(GameProofSubmissionOperationRow.State), nameof(GameProofSubmissionOperationRow.NextReconciliationAtUtc) }));
    }

    [Fact]
    public void Operation_HasConcurrencyToken()
    {
        using var db = new ModelOnlyContext();
        var entity = db.Model.FindEntityType(typeof(GameProofSubmissionOperationRow))!;
        Assert.True(entity.FindProperty(nameof(GameProofSubmissionOperationRow.RowVersion))!.IsConcurrencyToken);
    }

    [Fact]
    public void CanonicalMedia_HasRequiredLengthConstraint()
    {
        using var db = new ModelOnlyContext();
        var entity = db.Model.FindEntityType(typeof(GameProofSubmissionOperationRow))!;
        var property = entity.FindProperty(nameof(GameProofSubmissionOperationRow.CanonicalMediaId))!;
        Assert.False(property.IsNullable);
        Assert.Equal(256, property.GetMaxLength());
    }
    [Fact]
    public void Operation_HasTrustedAttemptOwnershipForeignKey()
    {
        using var db = new ModelOnlyContext();
        var entity = db.Model.FindEntityType(typeof(GameProofSubmissionOperationRow))!;
        Assert.Contains(entity.GetForeignKeys(), fk =>
            fk.PrincipalEntityType.ClrType == typeof(GameAttemptRow) &&
            fk.DeleteBehavior == DeleteBehavior.NoAction &&
            fk.Properties.Select(p => p.Name).SequenceEqual(new[] {
                nameof(GameProofSubmissionOperationRow.AttemptId),
                nameof(GameProofSubmissionOperationRow.GameId),
                nameof(GameProofSubmissionOperationRow.AuthenticatedPlayerUserId) }) &&
            fk.PrincipalKey.Properties.Select(p => p.Name).SequenceEqual(new[] {
                nameof(GameAttemptRow.Id),
                nameof(GameAttemptRow.GameId),
                nameof(GameAttemptRow.PlayerUserId) }));
    }
    [Fact]
    public void ProviderAndCapture_HaveUniqueIndex()
    {
        using var db = new ModelOnlyContext();
        var entity = db.Model.FindEntityType(typeof(GameProofSubmissionOperationRow))!;
        Assert.Contains(entity.GetIndexes(), index =>
            index.IsUnique &&
            index.GetDatabaseName() == "UX_GameProofOperations_Provider_Capture" &&
            index.Properties.Select(p => p.Name).SequenceEqual(new[] {
                nameof(GameProofSubmissionOperationRow.MediaProviderNamespace),
                nameof(GameProofSubmissionOperationRow.CaptureSessionId) }));
    }
    [Fact]
    public void Operation_HasRecoveryCheckConstraints()
    {
        using var db = new ModelOnlyContext();
        var entity = db.Model.FindEntityType(typeof(GameProofSubmissionOperationRow))!;
        var checks = db.GetService<Microsoft.EntityFrameworkCore.Metadata.IDesignTimeModel>().Model.FindEntityType(typeof(GameProofSubmissionOperationRow))!.GetCheckConstraints();
        Assert.Contains(checks, c => c.Name == "CK_GameProofOperations_State" && c.Sql == "[State] IN (0,1,2,3,4,5,6)");
        Assert.Contains(checks, c => c.Name == "CK_GameProofOperations_Step" && c.Sql == "[StepNumber] >= 1");
        Assert.Contains(checks, c => c.Name == "CK_GameProofOperations_RecoveryAttempts" && c.Sql == "[RecoveryAttemptCount] >= 0");
    }
    [Fact]
    public void ProviderReceipt_HasExpectedMapping()
    {
        using var db = new ModelOnlyContext();
        var entity = db.Model.FindEntityType(typeof(GameProofSubmissionOperationRow))!;
        var property = entity.FindProperty(nameof(GameProofSubmissionOperationRow.ProviderReceiptId));
        Assert.NotNull(property);
        Assert.Equal(256, property.GetMaxLength());
        Assert.True(property.IsNullable);
    }
}
