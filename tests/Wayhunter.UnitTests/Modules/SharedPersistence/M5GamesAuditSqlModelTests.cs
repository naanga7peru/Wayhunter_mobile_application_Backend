
using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Wayhunter.Infrastructure.Persistence.Configurations.Games;
using Wayhunter.Infrastructure.Persistence.Configurations.IdentitySocial;
using Wayhunter.Infrastructure.Persistence.Models.Games;
using Xunit;

namespace Wayhunter.UnitTests.Modules.SharedPersistence;

public sealed class M5GamesAuditSqlModelTests
{
    private sealed class AuditModelContext : DbContext
    {
        protected override void OnConfiguring(
            DbContextOptionsBuilder options)
        {
            // Model-only tests. No SQL connection opened.
            options.UseSqlServer(
                "Server=localhost;" +
                "Database=Wayhunter_ModelOnly;" +
                "Trusted_Connection=True;" +
                "TrustServerCertificate=True");
        }

        protected override void OnModelCreating(
            ModelBuilder model)
        {
            model.ApplyConfiguration(
                new UserAccountConfiguration());

            // Existing M5 Phase 1 configurations.
            model.ApplyConfiguration(
                new GameRowConfiguration());

            model.ApplyConfiguration(
                new GameStepRowConfiguration());

            model.ApplyConfiguration(
                new GameHintRowConfiguration());

            model.ApplyConfiguration(
                new GameAttemptRowConfiguration());

            model.ApplyConfiguration(
                new GameAttemptProgressRowConfiguration());

            model.ApplyConfiguration(
                new GameProofEvidenceRowConfiguration());

            model.ApplyConfiguration(
                new GameWinnerRowConfiguration());

            // New M5 Phase 2 configurations.
            model.ApplyConfiguration(
                new GameCreationApprovalRowConfiguration());

            model.ApplyConfiguration(
                new GameModerationDecisionRowConfiguration());

            model.ApplyConfiguration(
                new GameProofReviewDecisionRowConfiguration());

            model.ApplyConfiguration(
                new GameResultDecisionRowConfiguration());
        }
    }

    [Theory]
    [InlineData(
        typeof(GameCreationApprovalRow),
        "GameCreationApprovals")]
    [InlineData(
        typeof(GameModerationDecisionRow),
        "GameModerationDecisions")]
    [InlineData(
        typeof(GameProofReviewDecisionRow),
        "GameProofReviewDecisions")]
    [InlineData(
        typeof(GameResultDecisionRow),
        "GameResultDecisions")]
    public void AuditTables_AreMapped(
        Type type,
        string tableName)
    {
        using var db = new AuditModelContext();

        var entity = db.Model.FindEntityType(type);

        Assert.NotNull(entity);
        Assert.Equal(tableName, entity.GetTableName());
    }

    [Fact]
    public void CreatorApproval_IsUniquePerGame()
    {
        using var db = new AuditModelContext();

        var entity = db.Model.FindEntityType(
            typeof(GameCreationApprovalRow));

        Assert.NotNull(entity);

        Assert.Contains(entity.GetIndexes(), index =>
            index.IsUnique &&
            index.Properties.Single().Name ==
                nameof(GameCreationApprovalRow.GameId));
    }

    [Fact]
    public void CreatorRewardEvent_IsUnique()
    {
        using var db = new AuditModelContext();

        var entity = db.Model.FindEntityType(
            typeof(GameCreationApprovalRow));

        Assert.NotNull(entity);

        Assert.Contains(entity.GetIndexes(), index =>
            index.IsUnique &&
            index.Properties.Single().Name ==
                nameof(GameCreationApprovalRow.RewardEventId));
    }

    [Fact]
    public void OneApprovedResultPerGame_IsEnforced()
    {
        using var db = new AuditModelContext();

        var entity = db.Model.FindEntityType(
            typeof(GameResultDecisionRow));

        Assert.NotNull(entity);

        var index = Assert.Single(
            entity.GetIndexes(),
            x => x.IsUnique &&
                 x.Properties.Single().Name ==
                     nameof(GameResultDecisionRow.GameId));

        Assert.Equal(
            "[IsApproved] = 1",
            index.GetFilter());
    }

    [Fact]
    public void ProofReview_IsUniquePerEvidence()
    {
        using var db = new AuditModelContext();

        var entity = db.Model.FindEntityType(
            typeof(GameProofReviewDecisionRow));

        Assert.NotNull(entity);

        Assert.Contains(entity.GetIndexes(), index =>
            index.IsUnique &&
            index.Properties.Single().Name ==
                nameof(GameProofReviewDecisionRow.EvidenceId));
    }

    [Fact]
    public void AllAuditRelationships_DisableCascadeDelete()
    {
        using var db = new AuditModelContext();

        Type[] types =
        {
            typeof(GameCreationApprovalRow),
            typeof(GameModerationDecisionRow),
            typeof(GameProofReviewDecisionRow),
            typeof(GameResultDecisionRow)
        };

        foreach (Type type in types)
        {
            var entity = db.Model.FindEntityType(type);

            Assert.NotNull(entity);

            foreach (var fk in entity.GetForeignKeys())
            {
                Assert.Equal(
                    DeleteBehavior.NoAction,
                    fk.DeleteBehavior);
            }
        }
    }

    [Fact]
    public void GeneratedSql_ContainsAuditTables()
    {
        using var db = new AuditModelContext();

        string sql = db.Database.GenerateCreateScript();

        string[] tables =
        {
            "GameCreationApprovals",
            "GameModerationDecisions",
            "GameProofReviewDecisions",
            "GameResultDecisions"
        };

        foreach (string table in tables)
        {
            Assert.Contains(
                $"CREATE TABLE [{table}]",
                sql);
        }
    }

    [Fact]
    public void M5Model_HasNoDuplicateTables()
    {
        using var db = new AuditModelContext();

        var names = db.Model.GetEntityTypes()
            .Select(x => x.GetTableName())
            .Where(x => x is not null)
            .ToArray();

        Assert.Equal(
            names.Length,
            names.Distinct(
                StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void AuditCorrelationIds_AreUniqueForIdempotentRetries()
    {
        using var db = new AuditModelContext();

        Type[] types =
        {
            typeof(GameCreationApprovalRow),
            typeof(GameModerationDecisionRow),
            typeof(GameProofReviewDecisionRow),
            typeof(GameResultDecisionRow)
        };

        foreach (Type type in types)
        {
            var entity = db.Model.FindEntityType(type);

            Assert.NotNull(entity);
            Assert.Contains(entity.GetIndexes(), index =>
                index.IsUnique &&
                index.Properties.Single().Name == "CorrelationId");
        }
    }
}
