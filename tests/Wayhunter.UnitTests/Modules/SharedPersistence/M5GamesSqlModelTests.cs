
using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Wayhunter.Infrastructure.Persistence.Configurations.Games;
using Wayhunter.Infrastructure.Persistence.Configurations.IdentitySocial;
using Wayhunter.Infrastructure.Persistence.Models.Games;
using Xunit;

namespace Wayhunter.UnitTests.Modules.SharedPersistence;

public sealed class M5GamesSqlModelTests
{
    private sealed class GameModelContext : DbContext
    {
        protected override void OnConfiguring(
            DbContextOptionsBuilder options)
        {
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
                new GameCompletionResultRowConfiguration());

            model.ApplyConfiguration(
                new GameCompletionEvidenceRowConfiguration());

            model.ApplyConfiguration(
                new GameWinnerRowConfiguration());
        }
    }

    [Theory]
    [InlineData(typeof(GameRow), "Games")]
    [InlineData(typeof(GameStepRow), "GameSteps")]
    [InlineData(typeof(GameHintRow), "GameHints")]
    [InlineData(typeof(GameAttemptRow), "GameAttempts")]
    [InlineData(typeof(GameAttemptProgressRow), "GameAttemptProgress")]
    [InlineData(typeof(GameProofEvidenceRow), "GameProofEvidence")]
    [InlineData(typeof(GameCompletionResultRow), "GameCompletionResults")]
    [InlineData(typeof(GameCompletionEvidenceRow), "GameCompletionEvidence")]
    [InlineData(typeof(GameWinnerRow), "GameWinners")]
    public void M5Tables_AreMapped(
        Type entityType,
        string tableName)
    {
        using var db = new GameModelContext();

        var entity = db.Model.FindEntityType(entityType);

        Assert.NotNull(entity);
        Assert.Equal(tableName, entity.GetTableName());
    }

    [Fact]
    public void Game_HasCreatorForeignKey()
    {
        using var db = new GameModelContext();

        var entity = db.Model.FindEntityType(typeof(GameRow));

        Assert.NotNull(entity);

        Assert.Contains(entity.GetForeignKeys(), fk =>
            fk.PrincipalEntityType.ClrType ==
                typeof(UserAccount) &&
            fk.Properties.Single().Name ==
                nameof(GameRow.CreatorUserId));
    }

    [Fact]
    public void GameStep_HasUniqueStepNumberPerGame()
    {
        using var db = new GameModelContext();

        var entity = db.Model.FindEntityType(
            typeof(GameStepRow));

        Assert.NotNull(entity);

        Assert.Contains(entity.GetIndexes(), index =>
            index.IsUnique &&
            index.Properties.Select(x => x.Name)
                .SequenceEqual(new[]
                {
                    nameof(GameStepRow.GameId),
                    nameof(GameStepRow.StepNumber)
                }));
    }

    [Fact]
    public void GameWinner_EnforcesOneWinnerPerGame()
    {
        using var db = new GameModelContext();

        var entity = db.Model.FindEntityType(
            typeof(GameWinnerRow));

        Assert.NotNull(entity);

        Assert.Equal(
            nameof(GameWinnerRow.GameId),
            entity.FindPrimaryKey()!.Properties.Single().Name);

        Assert.Contains(entity.GetIndexes(), index =>
            index.IsUnique &&
            index.Properties.Single().Name ==
                nameof(GameWinnerRow.AttemptId));
    }

    [Fact]
    public void AttemptsAndEvidence_EnforceSameGameAndPlayerOwnership()
    {
        using var db = new GameModelContext();
        var attempt = db.Model.FindEntityType(typeof(GameAttemptRow))!;
        Assert.Contains(attempt.GetKeys(), key =>
            key.Properties.Select(p => p.Name).SequenceEqual(new[]
            {
                nameof(GameAttemptRow.Id), nameof(GameAttemptRow.GameId)
            }));
        Assert.Contains(attempt.GetKeys(), key =>
            key.Properties.Select(p => p.Name).SequenceEqual(new[]
            {
                nameof(GameAttemptRow.Id), nameof(GameAttemptRow.GameId),
                nameof(GameAttemptRow.PlayerUserId)
            }));

        var proof = db.Model.FindEntityType(typeof(GameProofEvidenceRow))!;
        Assert.Contains(proof.GetForeignKeys(), fk =>
            fk.Properties.Select(p => p.Name).SequenceEqual(new[]
                { nameof(GameProofEvidenceRow.AttemptId), nameof(GameProofEvidenceRow.GameId) }) &&
            fk.PrincipalKey.Properties.Select(p => p.Name).SequenceEqual(new[]
                { nameof(GameAttemptRow.Id), nameof(GameAttemptRow.GameId) }));
        Assert.Contains(proof.GetForeignKeys(), fk =>
            fk.Properties.Select(p => p.Name).SequenceEqual(new[]
                { nameof(GameProofEvidenceRow.GameId), nameof(GameProofEvidenceRow.StepNumber) }) &&
            fk.PrincipalEntityType.ClrType == typeof(GameStepRow));
    }

    [Fact]
    public void CompletionWinnerAndProgress_AreBoundToAttemptIdentity()
    {
        using var db = new GameModelContext();
        var result = db.Model.FindEntityType(typeof(GameCompletionResultRow))!;
        var winner = db.Model.FindEntityType(typeof(GameWinnerRow))!;
        var progress = db.Model.FindEntityType(typeof(GameAttemptProgressRow))!;

        Assert.Contains(result.GetForeignKeys(), fk =>
            fk.Properties.Select(p => p.Name).SequenceEqual(new[]
                { nameof(GameCompletionResultRow.AttemptId), nameof(GameCompletionResultRow.GameId), nameof(GameCompletionResultRow.PlayerUserId) }) &&
            fk.PrincipalKey.Properties.Select(p => p.Name).SequenceEqual(new[]
                { nameof(GameAttemptRow.Id), nameof(GameAttemptRow.GameId), nameof(GameAttemptRow.PlayerUserId) }));
        Assert.Contains(winner.GetForeignKeys(), fk =>
            fk.Properties.Select(p => p.Name).SequenceEqual(new[]
                { nameof(GameWinnerRow.AttemptId), nameof(GameWinnerRow.GameId), nameof(GameWinnerRow.PlayerUserId) }));
        Assert.Contains(progress.GetForeignKeys(), fk =>
            fk.Properties.Select(p => p.Name).SequenceEqual(new[]
                { nameof(GameAttemptProgressRow.AttemptId), nameof(GameAttemptProgressRow.GameId), nameof(GameAttemptProgressRow.PlayerUserId) }));
        Assert.Contains(progress.GetIndexes(), ix => ix.IsUnique &&
            ix.GetFilter() == "[Status] IN (0,1,2)" &&
            ix.Properties.Select(p => p.Name).SequenceEqual(new[]
                { nameof(GameAttemptProgressRow.GameId), nameof(GameAttemptProgressRow.PlayerUserId) }));
    }

    [Fact]
    public void CompletionEvidence_RequiresMatchingAttempt()
    {
        using var db = new GameModelContext();
        var evidence = db.Model.FindEntityType(typeof(GameCompletionEvidenceRow))!;
        Assert.Equal(2, evidence.GetForeignKeys().Count());
        Assert.All(evidence.GetForeignKeys(), fk =>
            Assert.Contains(nameof(GameCompletionEvidenceRow.AttemptId),
                fk.Properties.Select(p => p.Name)));
    }

    [Fact]
    public void GameAndAttempt_HaveConcurrencyTokens()
    {
        using var db = new GameModelContext();

        var game = db.Model.FindEntityType(typeof(GameRow));
        var attempt = db.Model.FindEntityType(
            typeof(GameAttemptRow));

        Assert.NotNull(game);
        Assert.NotNull(attempt);

        Assert.True(
            game.FindProperty(
                nameof(GameRow.RowVersion))!.IsConcurrencyToken);

        Assert.True(
            attempt.FindProperty(
                nameof(GameAttemptRow.RowVersion))!
                .IsConcurrencyToken);
    }

    [Fact]
    public void ProofEvidence_AllowsMultipleRetakes()
    {
        using var db = new GameModelContext();

        var entity = db.Model.FindEntityType(
            typeof(GameProofEvidenceRow));

        Assert.NotNull(entity);

        Assert.DoesNotContain(entity.GetIndexes(), index =>
            index.IsUnique &&
            index.Properties.Select(x => x.Name)
                .SequenceEqual(new[]
                {
                    nameof(GameProofEvidenceRow.AttemptId),
                    nameof(GameProofEvidenceRow.StepNumber)
                }));
    }

    [Fact]
    public void GeneratedSql_ContainsCurrentGamesTables()
    {
        using var db = new GameModelContext();

        string sql = db.Database.GenerateCreateScript();

        string[] tables =
        {
            "Games",
            "GameSteps",
            "GameHints",
            "GameAttempts",
            "GameAttemptProgress",
            "GameProofEvidence",
            "GameCompletionResults",
            "GameCompletionEvidence",
            "GameWinners"
        };

        foreach (string table in tables)
        {
            Assert.Contains($"CREATE TABLE [{table}]", sql);
        }
    }

    [Fact]
    public void GameRelationships_DoNotCascadeDelete()
    {
        using var db = new GameModelContext();

        Type[] types =
        {
            typeof(GameRow),
            typeof(GameStepRow),
            typeof(GameHintRow),
            typeof(GameAttemptRow),
            typeof(GameAttemptProgressRow),
            typeof(GameProofEvidenceRow),
            typeof(GameCompletionResultRow),
            typeof(GameCompletionEvidenceRow),
            typeof(GameWinnerRow)
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
    public void CompletionResult_IsConcurrencySafeAndUniquePerAttempt()
    {
        using var db = new GameModelContext();

        var entity = db.Model.FindEntityType(
            typeof(GameCompletionResultRow));

        Assert.NotNull(entity);
        Assert.True(
            entity.FindProperty(
                nameof(GameCompletionResultRow.RowVersion))!
                .IsConcurrencyToken);

        Assert.Contains(entity.GetIndexes(), index =>
            index.IsUnique &&
            index.Properties.Single().Name ==
                nameof(GameCompletionResultRow.AttemptId));
    }

    [Fact]
    public void CompletionEvidence_CannotBeReusedAcrossResults()
    {
        using var db = new GameModelContext();

        var entity = db.Model.FindEntityType(
            typeof(GameCompletionEvidenceRow));

        Assert.NotNull(entity);
        Assert.Contains(entity.GetIndexes(), index =>
            index.IsUnique &&
            index.Properties.Single().Name ==
                nameof(GameCompletionEvidenceRow.EvidenceId));
    }
}
