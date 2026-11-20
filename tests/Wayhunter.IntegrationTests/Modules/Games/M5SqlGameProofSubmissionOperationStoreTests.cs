
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Wayhunter.Application.Modules.Games.Dtos;
using Wayhunter.Application.Modules.Games.Interfaces;
using Wayhunter.Domain.Modules.Games.Entities;
using Wayhunter.Domain.Modules.Games.Enums;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;
using Wayhunter.Infrastructure.Persistence.DbContexts;
using Wayhunter.Infrastructure.Persistence.Models.Games;
using Wayhunter.Infrastructure.Persistence.Repositories.Games;
using Xunit;

namespace Wayhunter.IntegrationTests.Modules.Games;

public sealed class M5RecoverySqlIntegrationFactAttribute : FactAttribute
{
    public M5RecoverySqlIntegrationFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WAYHUNTER_M5_TEST_SQL_CONNECTION")) || !string.Equals(Environment.GetEnvironmentVariable("WAYHUNTER_M5_RECOVERY_SQL_AUTHORIZED"), "true", StringComparison.Ordinal))
            Skip = "M5 recovery SQL test connection and DB Coordinator authorization are required.";
    }
}

public sealed class M5SqlGameProofSubmissionOperationStoreTests
{
    private const string AllowedDatabase =
        "Wayhunter_M5_IntegrationTest";

    private const string AllowedServer =
        @"(localdb)\MSSQLLocalDB";

    private static WayhunterSharedDbContext CreateContext()
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

        if (!string.Equals(
                builder.InitialCatalog,
                AllowedDatabase,
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                builder.DataSource,
                AllowedServer,
                StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrWhiteSpace(builder.AttachDBFilename))
        {
            throw new InvalidOperationException(
                "Only the approved M5 LocalDB is allowed.");
        }

        var options =
            new DbContextOptionsBuilder<WayhunterSharedDbContext>()
                .UseSqlServer(connectionString)
                .Options;

        return new WayhunterSharedDbContext(options);
    }

    private sealed record TestSeed(
        Guid GameId,
        Guid AttemptId,
        string CreatorUserId,
        string PlayerUserId,
        DateTimeOffset StartedAtUtc,
        DateTimeOffset DeadlineUtc);

    private static async Task<TestSeed> SeedAsync()
    {
        // Existing approved schema only.
        // Never create or apply shared migrations.

        var approvedAtUtc =
            DateTimeOffset.UtcNow.AddMinutes(-2);

        var submittedAtUtc =
            approvedAtUtc.AddMinutes(-1);

        var creator = UserAccount.RegisterPending(
            "M5 Submission Creator",
            $"m5-sub-creator-{Guid.NewGuid():N}@example.invalid",
            "test-only-password-hash-placeholder",
            submittedAtUtc);

        creator.ActivateAfterVerifiedOtp(submittedAtUtc);

        var player = UserAccount.RegisterPending(
            "M5 Submission Player",
            $"m5-sub-player-{Guid.NewGuid():N}@example.invalid",
            "test-only-password-hash-placeholder",
            submittedAtUtc);

        player.ActivateAfterVerifiedOtp(submittedAtUtc);

        var game = Game.CreateDraft(
            Guid.NewGuid(),
            creator.UserId,
            "SQL Proof Submission Test Game",
            "Integration test only",
            60);

        var step1 = new GameStep(
            Guid.NewGuid(), game.Id, 1);

        var step2 = new GameStep(
            Guid.NewGuid(), game.Id, 2);

        game.ReplaceDraftStages(
            creator.UserId,
            new[] { step1, step2 },
            new[]
            {
                new GameHint(
                    Guid.NewGuid(),
                    step1.Id,
                    "Find first checkpoint",
                    null),
                new GameHint(
                    Guid.NewGuid(),
                    step2.Id,
                    "Find second checkpoint",
                    null)
            });

        game.SubmitForAdminReview(
            creator.UserId,
            submittedAtUtc);

        game.ApproveCreation(approvedAtUtc);

        try
        {
            await using var db = CreateContext();

            db.Users.AddRange(creator, player);
            await db.SaveChangesAsync();

            await new SqlGameRepository(db)
                .AddAsync(game);

            var startedAtUtc =
                approvedAtUtc.AddMinutes(1);

            var attempt =
                await new SqlGameAttemptRepository(db)
                    .StartOrResumeAsync(
                        game.Id,
                        player.UserId,
                        startedAtUtc);

            return new TestSeed(
                game.Id,
                attempt.AttemptId,
                creator.UserId,
                player.UserId,
                startedAtUtc,
                attempt.DeadlineUtc);
        }
        catch (Exception seedError)
        {
            try
            {
                await CleanupAsync(new TestSeed(game.Id, Guid.Empty, creator.UserId, player.UserId, default, default));
            }
            catch (Exception cleanupError)
            {
                throw new AggregateException("M5 test seed and cleanup both failed.", seedError, cleanupError);
            }
            throw;
        }
    }

    private static SubmitGameStageProofCommand Command(
        TestSeed seed)
    {
        return new SubmitGameStageProofCommand(
            seed.GameId,
            seed.AttemptId,
            1,
            $"m5-media-{Guid.NewGuid():N}",
            $"m5-capture-{Guid.NewGuid():N}");
    }

    private static DateTimeOffset ProofTime(TestSeed seed)
    {
        return seed.StartedAtUtc.AddSeconds(30);
    }

    private static async Task CleanupAsync(TestSeed seed)
    {
        await using var db = CreateContext();

        // Remove test recovery operations before deleting referenced GameAttempts.
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM [dbo].[GameProofSubmissionOperations] WHERE [GameId] = {seed.GameId}");

        // Delete children before parents.
        await db.GameProofReviewDecisions
            .Where(x => db.GameProofEvidence.Any(e =>
                e.Id == x.EvidenceId &&
                e.GameId == seed.GameId))
            .ExecuteDeleteAsync();

        await db.GameProofEvidence
            .Where(x => x.GameId == seed.GameId)
            .ExecuteDeleteAsync();

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


    [M5RecoverySqlIntegrationFact]
    public async Task UnknownOutcome_ShouldBlockNewInitialClaim()
    {
        var seed = await SeedAsync();
        try
        {
            var store = new SqlGameProofSubmissionOperationStore(Environment.GetEnvironmentVariable("WAYHUNTER_M5_TEST_SQL_CONNECTION")!);
            var binding = new GameProofSubmissionOperationBinding(Guid.NewGuid(), seed.PlayerUserId, seed.GameId, seed.AttemptId, 1, $"capture-{Guid.NewGuid():N}", "m5-test", $"media-{Guid.NewGuid():N}");
            await store.RegisterOrReadAsync(binding, DateTimeOffset.UtcNow);
            var originalToken = $"original-{Guid.NewGuid():N}";
            Assert.True(await store.TryClaimPendingAsync(binding, originalToken, DateTimeOffset.UtcNow.AddMinutes(5)));
            Assert.True(await store.TryMarkProviderOutcomeUnknownAsync(binding, originalToken));
            Assert.False(await store.TryClaimPendingAsync(binding, $"retry-{Guid.NewGuid():N}", DateTimeOffset.UtcNow.AddMinutes(5)));
            var snapshot = await store.GetAsync(binding.SubmissionOperationId);
            Assert.NotNull(snapshot);
            Assert.Equal(GameProofSubmissionOperationState.ProviderOutcomeUnknown, snapshot.State);
            Assert.Null(snapshot.ClaimToken);
        }
        finally
        {
            await CleanupAsync(seed);
        }
    }

    [M5RecoverySqlIntegrationFact]
    public async Task WrongPlayerBinding_ShouldFailCompositeFk()
    {
        var seed = await SeedAsync();
        try
        {
            var store = new SqlGameProofSubmissionOperationStore(Environment.GetEnvironmentVariable("WAYHUNTER_M5_TEST_SQL_CONNECTION")!);
            var binding = new GameProofSubmissionOperationBinding(Guid.NewGuid(), $"wrong-player-{Guid.NewGuid():N}", seed.GameId, seed.AttemptId, 1, $"capture-{Guid.NewGuid():N}", "m5-test", $"media-{Guid.NewGuid():N}");
            var error = await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(() => store.RegisterOrReadAsync(binding, DateTimeOffset.UtcNow));
            Assert.Equal(547, error.Number);
            Assert.Null(await store.GetAsync(binding.SubmissionOperationId));
        }
        finally
        {
            await CleanupAsync(seed);
        }
    }

    [M5RecoverySqlIntegrationFact]
    public async Task DuplicateCaptureSession_ShouldRejectSecondOperation()
    {
        var seed = await SeedAsync();
        try
        {
            var store = new SqlGameProofSubmissionOperationStore(Environment.GetEnvironmentVariable("WAYHUNTER_M5_TEST_SQL_CONNECTION")!);
            var binding = new GameProofSubmissionOperationBinding(Guid.NewGuid(), seed.PlayerUserId, seed.GameId, seed.AttemptId, 1, $"capture-{Guid.NewGuid():N}", "m5-test", $"media-{Guid.NewGuid():N}");
            await store.RegisterOrReadAsync(binding, DateTimeOffset.UtcNow);
            var duplicate = binding with { SubmissionOperationId = Guid.NewGuid(), CanonicalMediaId = $"media-{Guid.NewGuid():N}" };
            var error = await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(() => store.RegisterOrReadAsync(duplicate, DateTimeOffset.UtcNow));
            Assert.Contains(error.Number, new[] { 2601, 2627 });
        }
        finally
        {
            await CleanupAsync(seed);
        }
    }

    [M5RecoverySqlIntegrationFact]
    public async Task ConcurrentReconciliation_ShouldAllowOnlyOneWorker()
    {
        var seed = await SeedAsync();
        try
        {
            var store = new SqlGameProofSubmissionOperationStore(Environment.GetEnvironmentVariable("WAYHUNTER_M5_TEST_SQL_CONNECTION")!);
            var binding = new GameProofSubmissionOperationBinding(Guid.NewGuid(), seed.PlayerUserId, seed.GameId, seed.AttemptId, 1, $"capture-{Guid.NewGuid():N}", "m5-test", $"media-{Guid.NewGuid():N}");
            await store.RegisterOrReadAsync(binding, DateTimeOffset.UtcNow);
            var lease = DateTimeOffset.UtcNow.AddSeconds(4);
            Assert.True(await store.TryClaimPendingAsync(binding, $"initial-{Guid.NewGuid():N}", lease));
            var wait = lease - DateTimeOffset.UtcNow + TimeSpan.FromSeconds(2);
            if (wait > TimeSpan.Zero) await Task.Delay(wait);
            var now = DateTimeOffset.UtcNow;
            var token1 = $"recovery1-{Guid.NewGuid():N}";
            var token2 = $"recovery2-{Guid.NewGuid():N}";
            var results = await Task.WhenAll(store.TryClaimReconciliationAsync(binding, token1, now, now.AddMinutes(5)), store.TryClaimReconciliationAsync(binding, token2, now, now.AddMinutes(5)));
            Assert.NotEqual(results[0], results[1]);
            var snapshot = await store.GetAsync(binding.SubmissionOperationId);
            Assert.NotNull(snapshot);
            Assert.Equal(results[0] ? token1 : token2, snapshot.ClaimToken);
            Assert.Equal(1, snapshot.RecoveryAttemptCount);
        }
        finally
        {
            await CleanupAsync(seed);
        }
    }

    [M5RecoverySqlIntegrationFact]
    public async Task ExpiredLease_ShouldAllowRecoveryClaim()
    {
        var seed = await SeedAsync();
        try
        {
            var connection = Environment.GetEnvironmentVariable("WAYHUNTER_M5_TEST_SQL_CONNECTION")!;
            var store = new SqlGameProofSubmissionOperationStore(connection);
            var binding = new GameProofSubmissionOperationBinding(Guid.NewGuid(), seed.PlayerUserId, seed.GameId, seed.AttemptId, 1, $"capture-{Guid.NewGuid():N}", "m5-test", $"media-{Guid.NewGuid():N}");
            await store.RegisterOrReadAsync(binding, DateTimeOffset.UtcNow);
            var originalToken = $"original-{Guid.NewGuid():N}";
            var recoveryToken = $"recovery-{Guid.NewGuid():N}";
            var initialLease = DateTimeOffset.UtcNow.AddSeconds(4);
            Assert.True(await store.TryClaimPendingAsync(binding, originalToken, initialLease));
            var wait = initialLease - DateTimeOffset.UtcNow + TimeSpan.FromSeconds(2);
            if (wait > TimeSpan.Zero) await Task.Delay(wait);
            var recoveryNow = DateTimeOffset.UtcNow;
            Assert.True(await store.TryClaimReconciliationAsync(binding, recoveryToken, recoveryNow, recoveryNow.AddMinutes(5)));
            Assert.False(await store.TryMarkProviderOutcomeUnknownAsync(binding, originalToken));
            var snapshot = await store.GetAsync(binding.SubmissionOperationId);
            Assert.NotNull(snapshot);
            Assert.Equal(GameProofSubmissionOperationState.VerificationInFlight, snapshot.State);
            Assert.Equal(recoveryToken, snapshot.ClaimToken);
            Assert.Equal(1, snapshot.RecoveryAttemptCount);
        }
        finally
        {
            await CleanupAsync(seed);
        }
    }


    [M5RecoverySqlIntegrationFact]
    public async Task StaleClaimToken_ShouldNotChangeOperationState()
    {
        var seed = await SeedAsync();
        try
        {
            var connection = Environment.GetEnvironmentVariable("WAYHUNTER_M5_TEST_SQL_CONNECTION")!;
            var store = new SqlGameProofSubmissionOperationStore(connection);
            var binding = new GameProofSubmissionOperationBinding(Guid.NewGuid(), seed.PlayerUserId, seed.GameId, seed.AttemptId, 1, $"capture-{Guid.NewGuid():N}", "m5-test", $"media-{Guid.NewGuid():N}");
            await store.RegisterOrReadAsync(binding, DateTimeOffset.UtcNow);
            var validToken = $"worker-{Guid.NewGuid():N}";
            var staleToken = $"stale-{Guid.NewGuid():N}";
            Assert.True(await store.TryClaimPendingAsync(binding, validToken, DateTimeOffset.UtcNow.AddMinutes(5)));
            Assert.False(await store.TryMarkProviderOutcomeUnknownAsync(binding, staleToken));
            var before = await store.GetAsync(binding.SubmissionOperationId);
            Assert.NotNull(before);
            Assert.Equal(GameProofSubmissionOperationState.VerificationInFlight, before.State);
            Assert.Equal(validToken, before.ClaimToken);
            Assert.True(await store.TryMarkProviderOutcomeUnknownAsync(binding, validToken));
            var after = await store.GetAsync(binding.SubmissionOperationId);
            Assert.NotNull(after);
            Assert.Equal(GameProofSubmissionOperationState.ProviderOutcomeUnknown, after.State);
            Assert.Null(after.ClaimToken);
        }
        finally
        {
            await CleanupAsync(seed);
        }
    }


    [M5RecoverySqlIntegrationFact]
    public async Task ConcurrentInitialClaims_ShouldAllowOnlyOneWorker()
    {
        var seed = await SeedAsync();
        try
        {
            var connection = Environment.GetEnvironmentVariable("WAYHUNTER_M5_TEST_SQL_CONNECTION")!;
            var store = new SqlGameProofSubmissionOperationStore(connection);
            var binding = new GameProofSubmissionOperationBinding(Guid.NewGuid(), seed.PlayerUserId, seed.GameId, seed.AttemptId, 1, $"capture-{Guid.NewGuid():N}", "m5-test", $"media-{Guid.NewGuid():N}");
            await store.RegisterOrReadAsync(binding, DateTimeOffset.UtcNow);
            var token1 = $"worker-1-{Guid.NewGuid():N}";
            var token2 = $"worker-2-{Guid.NewGuid():N}";
            var lease = DateTimeOffset.UtcNow.AddMinutes(5);
            var results = await Task.WhenAll(store.TryClaimPendingAsync(binding, token1, lease), store.TryClaimPendingAsync(binding, token2, lease));
            Assert.NotEqual(results[0], results[1]);
            var snapshot = await store.GetAsync(binding.SubmissionOperationId);
            Assert.NotNull(snapshot);
            Assert.Equal(GameProofSubmissionOperationState.VerificationInFlight, snapshot.State);
            Assert.Equal(results[0] ? token1 : token2, snapshot.ClaimToken);
        }
        finally
        {
            await CleanupAsync(seed);
        }
    }


    [M5RecoverySqlIntegrationFact]
    public async Task CrossGameDuplicateCanonicalMedia_ShouldRejectSecondOperation()
    {
        var seed = await SeedAsync();
        TestSeed? other = null;
        try
        {
            other = await SeedAsync();
        }
        catch
        {
            await CleanupAsync(seed);
            throw;
        }
        try
        {
            var connection = Environment.GetEnvironmentVariable("WAYHUNTER_M5_TEST_SQL_CONNECTION")!;
            var store = new SqlGameProofSubmissionOperationStore(connection);
            var binding = new GameProofSubmissionOperationBinding(Guid.NewGuid(), seed.PlayerUserId, seed.GameId, seed.AttemptId, 1, $"capture-{Guid.NewGuid():N}", "m5-test", $"media-{Guid.NewGuid():N}");
            await store.RegisterOrReadAsync(binding, DateTimeOffset.UtcNow);
            var duplicate = binding with { SubmissionOperationId = Guid.NewGuid(), GameId = other.GameId, AttemptId = other.AttemptId, AuthenticatedPlayerUserId = other.PlayerUserId, CaptureSessionId = $"capture-{Guid.NewGuid():N}" };
            var error = await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(() => store.RegisterOrReadAsync(duplicate, DateTimeOffset.UtcNow));
            Assert.Contains(error.Number, new[] { 2601, 2627 });
        }
        finally
        {
            await CleanupAsync(other);
            await CleanupAsync(seed);
        }
    }


    [M5RecoverySqlIntegrationFact]
    public async Task DuplicateCanonicalMedia_ShouldRejectSecondOperation()
    {
        var seed = await SeedAsync();
        try
        {
            var connection = Environment.GetEnvironmentVariable("WAYHUNTER_M5_TEST_SQL_CONNECTION")!;
            var store = new SqlGameProofSubmissionOperationStore(connection);
            var binding = new GameProofSubmissionOperationBinding(Guid.NewGuid(), seed.PlayerUserId, seed.GameId, seed.AttemptId, 1, $"capture-{Guid.NewGuid():N}", "m5-test", $"media-{Guid.NewGuid():N}");
            await store.RegisterOrReadAsync(binding, DateTimeOffset.UtcNow);
            var duplicate = binding with { SubmissionOperationId = Guid.NewGuid(), CaptureSessionId = $"capture-{Guid.NewGuid():N}" };
            var error = await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(() => store.RegisterOrReadAsync(duplicate, DateTimeOffset.UtcNow));
            Assert.Contains(error.Number, new[] { 2601, 2627 });
        }
        finally
        {
            await CleanupAsync(seed);
        }
    }


    [M5RecoverySqlIntegrationFact]
    public async Task ConflictingBinding_ShouldRejectOperationIdReuse()
    {
        var seed = await SeedAsync();
        try
        {
            var connection = Environment.GetEnvironmentVariable("WAYHUNTER_M5_TEST_SQL_CONNECTION")!;
            var store = new SqlGameProofSubmissionOperationStore(connection);
            var binding = new GameProofSubmissionOperationBinding(Guid.NewGuid(), seed.PlayerUserId, seed.GameId, seed.AttemptId, 1, $"capture-{Guid.NewGuid():N}", "m5-test", $"media-{Guid.NewGuid():N}");
            await store.RegisterOrReadAsync(binding, DateTimeOffset.UtcNow);
            var conflicting = binding with { CanonicalMediaId = $"media-{Guid.NewGuid():N}" };
            await Assert.ThrowsAsync<InvalidOperationException>(() => store.RegisterOrReadAsync(conflicting, DateTimeOffset.UtcNow));
        }
        finally
        {
            await CleanupAsync(seed);
        }
    }


    [M5RecoverySqlIntegrationFact]
    public async Task ConcurrentRegistration_ShouldReturnSameOperation()
    {
        var seed = await SeedAsync();
        try
        {
            var connection = Environment.GetEnvironmentVariable("WAYHUNTER_M5_TEST_SQL_CONNECTION")!;
            var store = new SqlGameProofSubmissionOperationStore(connection);
            var binding = new GameProofSubmissionOperationBinding(Guid.NewGuid(), seed.PlayerUserId, seed.GameId, seed.AttemptId, 1, $"capture-{Guid.NewGuid():N}", "m5-test", $"media-{Guid.NewGuid():N}");
            var now = DateTimeOffset.UtcNow;
            var results = await Task.WhenAll(store.RegisterOrReadAsync(binding, now), store.RegisterOrReadAsync(binding, now));
            Assert.All(results, result => Assert.Equal(binding, result.Binding));
            Assert.Equal(results[0].Binding.SubmissionOperationId, results[1].Binding.SubmissionOperationId);
        }
        finally
        {
            await CleanupAsync(seed);
        }
    }

    [M5RecoverySqlIntegrationFact]
    public async Task MissingOperation_ShouldReturnNullFromIsolatedSql()
    {
        var connection = Environment.GetEnvironmentVariable("WAYHUNTER_M5_TEST_SQL_CONNECTION")!;
        var store = new SqlGameProofSubmissionOperationStore(connection);
        var result = await store.GetAsync(Guid.NewGuid());
        Assert.Null(result);
    }
}
