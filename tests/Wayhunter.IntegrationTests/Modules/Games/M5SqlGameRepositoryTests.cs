
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Wayhunter.Domain.Modules.Games.Entities;
using Wayhunter.Domain.Modules.Games.Enums;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Wayhunter.Infrastructure.Persistence.Repositories.Games;
using Xunit;

namespace Wayhunter.IntegrationTests.Modules.Games;

public sealed class M5SqlIntegrationFactAttribute : FactAttribute
{
    public M5SqlIntegrationFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(
            Environment.GetEnvironmentVariable(
                "WAYHUNTER_M5_TEST_SQL_CONNECTION")))
        {
            Skip = "M5 isolated SQL test database is not configured.";
        }
    }
}

public sealed class M5SqlGameRepositoryTests
{
    private const string AllowedDatabase =
        "Wayhunter_M5_IntegrationTest";

    private const string AllowedServer =
        @"(localdb)\MSSQLLocalDB";

    private static readonly SemaphoreSlim SchemaGate = new(1, 1);

    private static bool _schemaReady;

    private static string GetConnectionString()
    {
        var connectionString =
            Environment.GetEnvironmentVariable(
                "WAYHUNTER_M5_TEST_SQL_CONNECTION");

        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                "M5 SQL test connection is missing.");

        var builder =
            new SqlConnectionStringBuilder(connectionString);

        if (!string.Equals(
                builder.InitialCatalog,
                AllowedDatabase,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Only the dedicated M5 test database is allowed.");
        }

        if (!string.Equals(
                builder.DataSource,
                AllowedServer,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Only the approved LocalDB instance is allowed.");
        }

        return connectionString;
    }

    private static WayhunterSharedDbContext CreateContext()
    {
        var options =
            new DbContextOptionsBuilder<WayhunterSharedDbContext>()
                .UseSqlServer(GetConnectionString())
                .Options;

        return new WayhunterSharedDbContext(options);
    }

    private static async Task EnsureSchemaAsync()
    {
        await SchemaGate.WaitAsync();

        try
        {
            if (_schemaReady)
                return;

            await using var db = CreateContext();

            // Verify the existing SQL test schema without migration
            // Schema changes are owned by the DB Coordinator.
            // Only the isolated M5 test database is allowed.
            await db.Games.AsNoTracking().Select(x => x.Id).Take(1).ToListAsync();

            _schemaReady = true;
        }
        finally
        {
            SchemaGate.Release();
        }
    }

    private static async Task<(Guid GameId, string UserId)>
        SeedDraftAsync()
    {
        await EnsureSchemaAsync();

        var now = DateTimeOffset.UtcNow;

        var user = UserAccount.RegisterPending(
            "M5 SQL Test Creator",
            $"m5-sql-{Guid.NewGuid():N}@example.invalid",
            "test-only-password-hash-placeholder",
            now);

        user.ActivateAfterVerifiedOtp(now);

        var game = Game.CreateDraft(
            Guid.NewGuid(),
            user.UserId,
            "Original SQL Game",
            "Initial draft",
            60);

        var step = new GameStep(
            Guid.NewGuid(),
            game.Id,
            1);

        var hint = new GameHint(
            Guid.NewGuid(),
            step.Id,
            "Original hint",
            null);

        game.ReplaceDraftStages(
            user.UserId,
            new[] { step },
            new[] { hint });

        await using var db = CreateContext();

        db.Users.Add(user);
        await db.SaveChangesAsync();

        var repository = new SqlGameRepository(db);

        await repository.AddAsync(game);

        return (game.Id, user.UserId);
    }

    private static async Task CleanupAsync(
        Guid gameId,
        string userId)
    {
        await using var db = CreateContext();

        var stepIds = await db.GameSteps
            .Where(x => x.GameId == gameId)
            .Select(x => x.Id)
            .ToArrayAsync();

        await db.GameHints
            .Where(x => stepIds.Contains(x.GameStepId))
            .ExecuteDeleteAsync();

        await db.GameSteps
            .Where(x => x.GameId == gameId)
            .ExecuteDeleteAsync();

        await db.Games
            .Where(x => x.Id == gameId)
            .ExecuteDeleteAsync();

        await db.Users
            .Where(x => x.UserId == userId)
            .ExecuteDeleteAsync();
    }

    [M5SqlIntegrationFact]
    public async Task DraftCrud_ShouldPersistToRealSql()
    {
        var seed = await SeedDraftAsync();

        try
        {
            // READ
            await using (var db = CreateContext())
            {
                var repository = new SqlGameRepository(db);

                var game = await repository.GetByIdAsync(
                    seed.GameId);

                Assert.NotNull(game);
                Assert.Equal(GameStatus.Draft, game.Status);
                Assert.Equal("Original SQL Game", game.Title);
                Assert.Single(game.DraftSteps);
                Assert.Single(game.DraftHints);

                // UPDATE DETAILS
                game.UpdateDraft(
                    seed.UserId,
                    "Updated SQL Game",
                    "Updated description",
                    90);

                // REPLACE STAGES AND HINTS
                var newStep = new GameStep(
                    Guid.NewGuid(),
                    game.Id,
                    1);

                var newHint = new GameHint(
                    Guid.NewGuid(),
                    newStep.Id,
                    "Updated hint",
                    null);

                game.ReplaceDraftStages(
                    seed.UserId,
                    new[] { newStep },
                    new[] { newHint });

                await repository.UpdateAsync(game);
            }

            // VERIFY IN INDEPENDENT DB CONTEXT
            await using (var db = CreateContext())
            {
                var repository = new SqlGameRepository(db);

                var updated = await repository.GetByIdAsync(
                    seed.GameId);

                Assert.NotNull(updated);
                Assert.Equal("Updated SQL Game", updated.Title);
                Assert.Equal(90, updated.DurationMinutes);
                Assert.Single(updated.DraftSteps);
                Assert.Single(updated.DraftHints);
                Assert.Equal(
                    "Updated hint",
                    updated.DraftHints[0].TextHint);

                // DELETE
                await repository.DeleteAsync(seed.GameId);

                Assert.Null(
                    await repository.GetByIdAsync(seed.GameId));
            }
        }
        finally
        {
            await CleanupAsync(seed.GameId, seed.UserId);
        }
    }

    [M5SqlIntegrationFact]
    public async Task StaleUpdate_ShouldBeRejected_AndPreserveLatestSqlState()
    {
        var seed = await SeedDraftAsync();

        try
        {
            // Two independent database contexts simulate
            // two requests reading the same game.
            await using var db1 = CreateContext();
            await using var db2 = CreateContext();

            var repository1 = new SqlGameRepository(db1);
            var repository2 = new SqlGameRepository(db2);

            var game1 = await repository1.GetByIdAsync(
                seed.GameId);

            var game2 = await repository2.GetByIdAsync(
                seed.GameId);

            Assert.NotNull(game1);
            Assert.NotNull(game2);

            // First request updates successfully.
            game1.UpdateDraft(
                seed.UserId,
                "First Request Update",
                "Updated by request one",
                60);

            await repository1.UpdateAsync(game1);

            // Second request still has the old RowVersion.
            game2.UpdateDraft(
                seed.UserId,
                "Stale Request Update",
                "This must not overwrite request one",
                90);

            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
                () => repository2.UpdateAsync(game2));

            // Verify that the successful update was preserved.
            await using var verifyDb = CreateContext();

            var verifyRepository =
                new SqlGameRepository(verifyDb);

            var finalGame = await verifyRepository.GetByIdAsync(
                seed.GameId);

            Assert.NotNull(finalGame);

            Assert.Equal(
                "First Request Update",
                finalGame.Title);

            Assert.Equal(
                60,
                finalGame.DurationMinutes);
        }
        finally
        {
            await CleanupAsync(
                seed.GameId,
                seed.UserId);
        }
    }

    [M5SqlIntegrationFact]
    public async Task DraftStageReplacement_WhenSqlInsertFails_ShouldRollback()
    {
        var seed = await SeedDraftAsync();

        try
        {
            await using (var db = CreateContext())
            {
                var repository = new SqlGameRepository(db);

                var game = await repository.GetByIdAsync(
                    seed.GameId);

                Assert.NotNull(game);

                var replacementStep = new GameStep(
                    Guid.NewGuid(),
                    game.Id,
                    1);

                // SQL GameHints.TextHint has max length 2000.
                // Force a database insert failure after
                // the old hints and steps are removed.
                var invalidHint = new GameHint(
                    Guid.NewGuid(),
                    replacementStep.Id,
                    new string('X', 2001),
                    null);

                game.ReplaceDraftStages(
                    seed.UserId,
                    new[] { replacementStep },
                    new[] { invalidHint });

                await Assert.ThrowsAsync<DbUpdateException>(
                    () => repository.UpdateAsync(game));
            }

            // A separate context verifies that the
            // SQL transaction rolled back completely.
            await using var verifyDb = CreateContext();

            var verifyRepository =
                new SqlGameRepository(verifyDb);

            var restored = await verifyRepository.GetByIdAsync(
                seed.GameId);

            Assert.NotNull(restored);

            Assert.Equal(
                "Original SQL Game",
                restored.Title);

            Assert.Single(restored.DraftSteps);
            Assert.Single(restored.DraftHints);

            Assert.Equal(
                "Original hint",
                restored.DraftHints[0].TextHint);
        }
        finally
        {
            await CleanupAsync(
                seed.GameId,
                seed.UserId);
        }
    }

    [M5SqlIntegrationFact]
    public async Task NonDraftGameDeletion_ShouldBeRejected()
    {
        var seed = await SeedDraftAsync();

        try
        {
            // Change the draft to PendingApproval.
            await using (var db = CreateContext())
            {
                var repository = new SqlGameRepository(db);

                var game = await repository.GetByIdAsync(
                    seed.GameId);

                Assert.NotNull(game);

                game.SubmitForAdminReview(
                    seed.UserId,
                    DateTimeOffset.UtcNow);

                await repository.UpdateAsync(game);
            }

            // Attempt deletion using a new DbContext.
            await using (var db = CreateContext())
            {
                var repository = new SqlGameRepository(db);

                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => repository.DeleteAsync(seed.GameId));
            }

            // Verify SQL data was not deleted.
            await using (var db = CreateContext())
            {
                var repository = new SqlGameRepository(db);

                var game = await repository.GetByIdAsync(
                    seed.GameId);

                Assert.NotNull(game);
                Assert.Equal(
                    GameStatus.PendingApproval,
                    game.Status);

                Assert.Single(game.DraftSteps);
                Assert.Single(game.DraftHints);
            }
        }
        finally
        {
            await CleanupAsync(
                seed.GameId,
                seed.UserId);
        }
    }

    [M5SqlIntegrationFact]
    public async Task SimultaneousUpdates_OnlyOneShouldSucceed()
    {
        var seed = await SeedDraftAsync();

        try
        {
            // Two separate contexts simulate independent requests.
            await using var db1 = CreateContext();
            await using var db2 = CreateContext();

            var repository1 = new SqlGameRepository(db1);
            var repository2 = new SqlGameRepository(db2);

            // Both requests load the same original RowVersion.
            var game1 = await repository1.GetByIdAsync(
                seed.GameId);

            var game2 = await repository2.GetByIdAsync(
                seed.GameId);

            Assert.NotNull(game1);
            Assert.NotNull(game2);

            game1.UpdateDraft(
                seed.UserId,
                "Concurrent Update One",
                "First request",
                60);

            game2.UpdateDraft(
                seed.UserId,
                "Concurrent Update Two",
                "Second request",
                90);

            // Release both requests from a shared start gate.
            var start = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

            async Task<bool> TrySaveAsync(
                SqlGameRepository repository,
                Game game)
            {
                await start.Task;

                try
                {
                    await repository.UpdateAsync(game);
                    return true;
                }
                catch (DbUpdateConcurrencyException)
                {
                    return false;
                }
            }

            var task1 = TrySaveAsync(repository1, game1);
            var task2 = TrySaveAsync(repository2, game2);

            start.SetResult();

            var results = await Task.WhenAll(task1, task2);

            // Exactly one request should succeed.
            Assert.Equal(1, results.Count(x => x));
            Assert.Equal(1, results.Count(x => !x));

            // Verify the database contains the winning update.
            await using var verifyDb = CreateContext();

            var verifyRepository =
                new SqlGameRepository(verifyDb);

            var finalGame = await verifyRepository.GetByIdAsync(
                seed.GameId);

            Assert.NotNull(finalGame);

            var expectedTitle = results[0]
                ? "Concurrent Update One"
                : "Concurrent Update Two";

            var expectedDuration = results[0] ? 60 : 90;

            Assert.Equal(expectedTitle, finalGame.Title);
            Assert.Equal(
                expectedDuration,
                finalGame.DurationMinutes);
        }
        finally
        {
            await CleanupAsync(
                seed.GameId,
                seed.UserId);
        }
    }

    [M5SqlIntegrationFact]
    public async Task WinnerChange_ThroughGenericRepository_ShouldBeRejected()
    {
        var seed = await SeedDraftAsync();

        try
        {
            await using (var db = CreateContext())
            {
                var repository = new SqlGameRepository(db);

                var game = await repository.GetByIdAsync(
                    seed.GameId);

                Assert.NotNull(game);

                var now = DateTimeOffset.UtcNow;

                // Move the game through the normal
                // submission and approval lifecycle.
                game.SubmitForAdminReview(
                    seed.UserId,
                    now.AddMinutes(-2));

                await repository.UpdateAsync(game);

                game.ApproveCreation(
                    now.AddMinutes(-1));

                await repository.UpdateAsync(game);

                // Domain winner approval changes
                // WinnerUserId, but generic SQL repository
                // must not create a GameWinnerRow.
                var playerUserId =
                    $"player-{Guid.NewGuid():N}";

                game.ApproveVerifiedWinner(
                    playerUserId,
                    now);

                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => repository.UpdateAsync(game));
            }

            // Verify the rejected operation did not
            // create a winner in SQL.
            await using var verifyDb = CreateContext();

            var winnerExists = await verifyDb.GameWinners
                .AsNoTracking()
                .AnyAsync(x => x.GameId == seed.GameId);

            Assert.False(winnerExists);

            var storedGame = await verifyDb.Games
                .AsNoTracking()
                .SingleAsync(x => x.Id == seed.GameId);

            Assert.Equal(
                GameStatus.Published,
                storedGame.Status);
        }
        finally
        {
            await CleanupAsync(
                seed.GameId,
                seed.UserId);
        }
    }

}
