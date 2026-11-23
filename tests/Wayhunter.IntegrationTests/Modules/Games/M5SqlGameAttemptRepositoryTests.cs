
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

public sealed class M5SqlGameAttemptRepositoryTests
{
    private const string AllowedDatabasePrefix = "Wayhunter_Test_";

    private const string AllowedServer =
        @"(localdb)\MSSQLLocalDB";

    private static string GetConnectionString()
    {
        var connectionString =
            Environment.GetEnvironmentVariable(
                "WAYHUNTER_M5_TEST_SQL_CONNECTION");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "M5 isolated SQL test connection is missing.");
        }

        var builder =
            new SqlConnectionStringBuilder(connectionString);

        if (!builder.InitialCatalog.StartsWith(
            AllowedDatabasePrefix,
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

    private sealed record TestSeed(
        Guid GameId,
        string CreatorUserId,
        string PlayerUserId,
        DateTimeOffset ApprovedAtUtc);

    private static async Task<TestSeed> SeedPublishedGameAsync()
    {
        // This test does not apply migrations.
        // The dedicated test database must already
        // contain the approved shared schema.

        var approvedAtUtc =
            DateTimeOffset.UtcNow.AddMinutes(-2);

        var submittedAtUtc =
            approvedAtUtc.AddMinutes(-1);

        var creator = UserAccount.RegisterPending(
            "M5 SQL Attempt Creator",
            $"m5-creator-{Guid.NewGuid():N}@example.invalid",
            "test-only-password-hash-placeholder",
            submittedAtUtc);

        creator.ActivateAfterVerifiedOtp(submittedAtUtc);

        var player = UserAccount.RegisterPending(
            "M5 SQL Attempt Player",
            $"m5-player-{Guid.NewGuid():N}@example.invalid",
            "test-only-password-hash-placeholder",
            submittedAtUtc);

        player.ActivateAfterVerifiedOtp(submittedAtUtc);

        var game = Game.CreateDraft(
            Guid.NewGuid(),
            creator.UserId,
            "SQL Attempt Test Game",
            "Integration test only",
            60);

        var step = new GameStep(
            Guid.NewGuid(),
            game.Id,
            1);

        var hint = new GameHint(
            Guid.NewGuid(),
            step.Id,
            "Find the first checkpoint",
            null);

        game.ReplaceDraftStages(
            creator.UserId,
            new[] { step },
            new[] { hint });

        game.SubmitForAdminReview(
            creator.UserId,
            submittedAtUtc);

        game.ApproveCreation(approvedAtUtc);

        await using var db = CreateContext();

        db.Users.AddRange(creator, player);
        await db.SaveChangesAsync();

        var repository = new SqlGameRepository(db);
        await repository.AddAsync(game);

        return new TestSeed(
            game.Id,
            creator.UserId,
            player.UserId,
            approvedAtUtc);
    }

    private static async Task CleanupAsync(TestSeed seed)
    {
        await using var db = CreateContext();

        // Child rows must be removed before parents.
        // Foreign keys use DeleteBehavior.NoAction.

        await db.GameAttemptProgress
            .Where(x => x.GameId == seed.GameId)
            .ExecuteDeleteAsync();

        await db.GameAttempts
            .Where(x => x.GameId == seed.GameId)
            .ExecuteDeleteAsync();

        var stepIds = await db.GameSteps
            .Where(x => x.GameId == seed.GameId)
            .Select(x => x.Id)
            .ToArrayAsync();

        await db.GameHints
            .Where(x => stepIds.Contains(x.GameStepId))
            .ExecuteDeleteAsync();

        await db.GameSteps
            .Where(x => x.GameId == seed.GameId)
            .ExecuteDeleteAsync();

        await db.Games
            .Where(x => x.Id == seed.GameId)
            .ExecuteDeleteAsync();

        await db.Users
            .Where(x =>
                x.UserId == seed.CreatorUserId ||
                x.UserId == seed.PlayerUserId)
            .ExecuteDeleteAsync();
    }

    [M5SqlIntegrationFact]
    public async Task Start_ShouldPersistAttemptAndProgressToSql()
    {
        var seed = await SeedPublishedGameAsync();

        try
        {
            var nowUtc =
                seed.ApprovedAtUtc.AddMinutes(1);

            Guid attemptId;

            await using (var db = CreateContext())
            {
                var repository =
                    new SqlGameAttemptRepository(db);

                var result = await repository.StartOrResumeAsync(
                    seed.GameId,
                    $"  {seed.PlayerUserId}  ",
                    nowUtc);

                attemptId = result.AttemptId;

                Assert.NotEqual(Guid.Empty, attemptId);
                Assert.Equal(seed.GameId, result.GameId);
                Assert.Equal(seed.PlayerUserId, result.PlayerUserId);
                Assert.Equal(nowUtc, result.StartedAtUtc);
                Assert.Equal(nowUtc.AddMinutes(60), result.DeadlineUtc);
                Assert.Equal(1, result.CurrentStepNumber);
                Assert.False(result.ResumedExistingAttempt);
            }

            // Verify persistence in an independent DbContext.
            await using (var db = CreateContext())
            {
                var attempt = await db.GameAttempts
                    .AsNoTracking()
                    .SingleAsync(x => x.Id == attemptId);

                var progress = await db.GameAttemptProgress
                    .AsNoTracking()
                    .SingleAsync(x => x.AttemptId == attemptId);

                Assert.Equal(seed.GameId, attempt.GameId);
                Assert.Equal(seed.PlayerUserId, attempt.PlayerUserId);
                Assert.Equal(nowUtc, attempt.StartedAtUtc);
                Assert.Equal(nowUtc.AddMinutes(60), attempt.DeadlineUtc);

                Assert.Equal(seed.GameId, progress.GameId);
                Assert.Equal(seed.PlayerUserId, progress.PlayerUserId);
                Assert.Equal(1, progress.TotalSteps);
                Assert.Equal(1, progress.CurrentStepNumber);
                Assert.Equal(0, progress.RejectedEvidenceCount);
                Assert.Equal(
                    GameProgressStatus.InProgress,
                    progress.Status);
            }
        }
        finally
        {
            await CleanupAsync(seed);
        }
    }

    [M5SqlIntegrationFact]
    public async Task Resume_AcrossDbContexts_ShouldPreserveOriginalDeadline()
    {
        var seed = await SeedPublishedGameAsync();

        try
        {
            var startedAtUtc =
                seed.ApprovedAtUtc.AddMinutes(1);

            var reconnectAtUtc =
                startedAtUtc.AddMinutes(15);

            Guid originalAttemptId;
            DateTimeOffset originalDeadline;

            await using (var db = CreateContext())
            {
                var repository =
                    new SqlGameAttemptRepository(db);

                var first = await repository.StartOrResumeAsync(
                    seed.GameId,
                    seed.PlayerUserId,
                    startedAtUtc);

                originalAttemptId = first.AttemptId;
                originalDeadline = first.DeadlineUtc;

                Assert.False(first.ResumedExistingAttempt);
            }

            // New context simulates a new API request
            // or an application restart.
            await using (var db = CreateContext())
            {
                var repository =
                    new SqlGameAttemptRepository(db);

                var resumed = await repository.StartOrResumeAsync(
                    seed.GameId,
                    seed.PlayerUserId,
                    reconnectAtUtc);

                Assert.True(resumed.ResumedExistingAttempt);
                Assert.Equal(originalAttemptId, resumed.AttemptId);
                Assert.Equal(startedAtUtc, resumed.StartedAtUtc);
                Assert.Equal(originalDeadline, resumed.DeadlineUtc);
            }

            await using (var db = CreateContext())
            {
                Assert.Equal(
                    1,
                    await db.GameAttempts.CountAsync(
                        x => x.GameId == seed.GameId &&
                             x.PlayerUserId == seed.PlayerUserId));

                Assert.Equal(
                    1,
                    await db.GameAttemptProgress.CountAsync(
                        x => x.GameId == seed.GameId &&
                             x.PlayerUserId == seed.PlayerUserId));
            }
        }
        finally
        {
            await CleanupAsync(seed);
        }
    }

    [M5SqlIntegrationFact]
    public async Task ConcurrentStarts_ShouldCreateExactlyOneAttempt()
    {
        var seed = await SeedPublishedGameAsync();

        try
        {
            var nowUtc =
                seed.ApprovedAtUtc.AddMinutes(1);

            var startGate = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

            var tasks = Enumerable.Range(0, 12)
                .Select(async _ =>
                {
                    await using var db = CreateContext();

                    var repository =
                        new SqlGameAttemptRepository(db);

                    await startGate.Task;

                    return await repository.StartOrResumeAsync(
                        seed.GameId,
                        seed.PlayerUserId,
                        nowUtc);
                })
                .ToArray();

            startGate.SetResult();

            var results = await Task.WhenAll(tasks);

            Assert.Equal(
                1,
                results.Count(x => !x.ResumedExistingAttempt));

            Assert.Equal(
                11,
                results.Count(x => x.ResumedExistingAttempt));

            Assert.Single(
                results.Select(x => x.AttemptId).Distinct());

            Assert.Single(
                results.Select(x => x.DeadlineUtc).Distinct());

            await using var verifyDb = CreateContext();

            Assert.Equal(
                1,
                await verifyDb.GameAttempts.CountAsync(
                    x => x.GameId == seed.GameId &&
                         x.PlayerUserId == seed.PlayerUserId));

            Assert.Equal(
                1,
                await verifyDb.GameAttemptProgress.CountAsync(
                    x => x.GameId == seed.GameId &&
                         x.PlayerUserId == seed.PlayerUserId));
        }
        finally
        {
            await CleanupAsync(seed);
        }
    }

    [M5SqlIntegrationFact]
    public async Task CompletedAttempt_ShouldNotReceiveNewAttempt()
    {
        var seed = await SeedPublishedGameAsync();

        try
        {
            var startedAtUtc =
                seed.ApprovedAtUtc.AddMinutes(1);

            Guid attemptId;

            await using (var db = CreateContext())
            {
                var repository =
                    new SqlGameAttemptRepository(db);

                var result = await repository.StartOrResumeAsync(
                    seed.GameId,
                    seed.PlayerUserId,
                    startedAtUtc);

                attemptId = result.AttemptId;
            }

            // Simulate a persisted terminal progress state.
            await using (var db = CreateContext())
            {
                var progress = await db.GameAttemptProgress
                    .SingleAsync(x => x.AttemptId == attemptId);

                progress.Status = GameProgressStatus.Completed;

                await db.SaveChangesAsync();
            }

            await using (var db = CreateContext())
            {
                var repository =
                    new SqlGameAttemptRepository(db);

                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => repository.StartOrResumeAsync(
                        seed.GameId,
                        seed.PlayerUserId,
                        startedAtUtc.AddMinutes(5)));
            }

            await using (var db = CreateContext())
            {
                Assert.Equal(
                    1,
                    await db.GameAttempts.CountAsync(
                        x => x.GameId == seed.GameId &&
                             x.PlayerUserId == seed.PlayerUserId));
            }
        }
        finally
        {
            await CleanupAsync(seed);
        }
    }

    [M5SqlIntegrationFact]
    public async Task StartNearExpiry_ShouldCapDeadlineAtGameExpiry()
    {
        var seed = await SeedPublishedGameAsync();

        try
        {
            var gameExpiry =
                seed.ApprovedAtUtc.AddHours(720);

            var startedAtUtc =
                gameExpiry.AddMinutes(-15);

            await using var db = CreateContext();

            var repository =
                new SqlGameAttemptRepository(db);

            var result = await repository.StartOrResumeAsync(
                seed.GameId,
                seed.PlayerUserId,
                startedAtUtc);

            Assert.Equal(gameExpiry, result.DeadlineUtc);
            Assert.Equal(
                TimeSpan.FromMinutes(15),
                result.DeadlineUtc - result.StartedAtUtc);
        }
        finally
        {
            await CleanupAsync(seed);
        }
    }

    [M5SqlIntegrationFact]
    public async Task Creator_ShouldNotStartOwnGame()
    {
        var seed = await SeedPublishedGameAsync();

        try
        {
            await using var db = CreateContext();

            var repository =
                new SqlGameAttemptRepository(db);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => repository.StartOrResumeAsync(
                    seed.GameId,
                    seed.CreatorUserId,
                    seed.ApprovedAtUtc.AddMinutes(1)));

            Assert.False(
                await db.GameAttempts.AnyAsync(
                    x => x.GameId == seed.GameId));
        }
        finally
        {
            await CleanupAsync(seed);
        }
    }

    [M5SqlIntegrationFact]
    public async Task CancelledStart_ShouldNotPersistAttempt()
    {
        var seed = await SeedPublishedGameAsync();

        try
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await using var db = CreateContext();

            var repository =
                new SqlGameAttemptRepository(db);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => repository.StartOrResumeAsync(
                    seed.GameId,
                    seed.PlayerUserId,
                    seed.ApprovedAtUtc.AddMinutes(1),
                    cts.Token));

            Assert.False(
                await db.GameAttempts.AnyAsync(
                    x => x.GameId == seed.GameId));
        }
        finally
        {
            await CleanupAsync(seed);
        }
    }

    [M5SqlIntegrationFact]
    public async Task Resume_AfterDeadline_ShouldNotCreateNewAttempt()
    {
        var seed = await SeedPublishedGameAsync();

        try
        {
            var startedAtUtc =
                seed.ApprovedAtUtc.AddMinutes(1);

            Guid originalAttemptId;
            DateTimeOffset deadlineUtc;

            // Start the original attempt.
            await using (var db = CreateContext())
            {
                var repository =
                    new SqlGameAttemptRepository(db);

                var first = await repository.StartOrResumeAsync(
                    seed.GameId,
                    seed.PlayerUserId,
                    startedAtUtc);

                originalAttemptId = first.AttemptId;
                deadlineUtc = first.DeadlineUtc;

                Assert.False(first.ResumedExistingAttempt);
            }

            // Simulate reconnecting after the deadline.
            await using (var db = CreateContext())
            {
                var repository =
                    new SqlGameAttemptRepository(db);

                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => repository.StartOrResumeAsync(
                        seed.GameId,
                        seed.PlayerUserId,
                        deadlineUtc.AddSeconds(1)));
            }

            // Verify the original attempt still exists,
            // and a new attempt was not created.
            await using (var db = CreateContext())
            {
                var attempt = await db.GameAttempts
                    .AsNoTracking()
                    .SingleAsync(x =>
                        x.GameId == seed.GameId &&
                        x.PlayerUserId == seed.PlayerUserId);

                Assert.Equal(originalAttemptId, attempt.Id);
                Assert.Equal(deadlineUtc, attempt.DeadlineUtc);

                Assert.Equal(
                    1,
                    await db.GameAttemptProgress.CountAsync(x =>
                        x.GameId == seed.GameId &&
                        x.PlayerUserId == seed.PlayerUserId));
            }
        }
        finally
        {
            await CleanupAsync(seed);
        }
    }

}
