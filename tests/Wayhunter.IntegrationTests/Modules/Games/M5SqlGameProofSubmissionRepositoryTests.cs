
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

public sealed class M5SqlGameProofSubmissionRepositoryTests
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
                StringComparison.OrdinalIgnoreCase))
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

    // Test fake only. Never register this in production.
    private sealed class TestCaptureVerifier
        : IGameProofCaptureVerifier
    {
        private readonly bool _allow;
        private int _calls;

        public TestCaptureVerifier(bool allow = true)
        {
            _allow = allow;
        }

        public int Calls => Volatile.Read(ref _calls);

        public Task<bool> VerifyAndConsumeAsync(
            string captureSessionId,
            string mediaId,
            Guid gameId,
            Guid attemptId,
            int stepNumber,
            string authenticatedPlayerUserId,
            DateTimeOffset nowUtc,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Interlocked.Increment(ref _calls);

            return Task.FromResult(_allow);
        }
    }

    private sealed class ThrowingCaptureVerifier
    : IGameProofCaptureVerifier
    {
        public Task<bool> VerifyAndConsumeAsync(
            string captureSessionId,
            string mediaId,
            Guid gameId,
            Guid attemptId,
            int stepNumber,
            string authenticatedPlayerUserId,
            DateTimeOffset nowUtc,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException(
                "Simulated trusted capture verifier failure.");
        }
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

    [M5SqlIntegrationFact]
    public async Task Submit_ShouldPersistEvidenceAndWaitingStatus()
    {
        var seed = await SeedAsync();

        try
        {
            var verifier = new TestCaptureVerifier();
            var command = Command(seed);
            var nowUtc = ProofTime(seed);

            Guid evidenceId;

            await using (var db = CreateContext())
            {
                var repository =
                    new SqlGameProofSubmissionRepository(
                        db, verifier);

                var result =
                    await repository.SubmitCurrentStageProofAsync(
                        command,
                        seed.PlayerUserId,
                        nowUtc);

                evidenceId = result.Id;

                Assert.NotEqual(Guid.Empty, evidenceId);
                Assert.Equal(seed.AttemptId, result.AttemptId);
                Assert.Equal(1, result.StepNumber);
                Assert.Equal(command.MediaId, result.MediaId);
                Assert.Equal(nowUtc, result.SubmittedAtUtc);
            }

            await using var verifyDb = CreateContext();

            var evidence = await verifyDb.GameProofEvidence
                .AsNoTracking()
                .SingleAsync(x => x.Id == evidenceId);

            var progress = await verifyDb.GameAttemptProgress
                .AsNoTracking()
                .SingleAsync(x =>
                    x.AttemptId == seed.AttemptId);

            Assert.Equal(seed.GameId, evidence.GameId);
            Assert.Equal(command.MediaId, evidence.MediaId);
            Assert.Equal(
                GameProgressStatus.WaitingForVerification,
                progress.Status);
            Assert.Equal(1, progress.CurrentStepNumber);
            Assert.Equal(1, verifier.Calls);
        }
        finally
        {
            await CleanupAsync(seed);
        }
    }

    [M5SqlIntegrationFact]
    public async Task InvalidCapture_ShouldRollbackSqlChanges()
    {
        var seed = await SeedAsync();

        try
        {
            var verifier = new TestCaptureVerifier(
                allow: false);

            await using (var db = CreateContext())
            {
                var repository =
                    new SqlGameProofSubmissionRepository(
                        db, verifier);

                await Assert.ThrowsAsync<
                    UnauthorizedAccessException>(
                    () => repository
                        .SubmitCurrentStageProofAsync(
                            Command(seed),
                            seed.PlayerUserId,
                            ProofTime(seed)));
            }

            await using var verifyDb = CreateContext();

            Assert.Equal(
                0,
                await verifyDb.GameProofEvidence.CountAsync(
                    x => x.GameId == seed.GameId));

            var progress = await verifyDb.GameAttemptProgress
                .AsNoTracking()
                .SingleAsync(x =>
                    x.AttemptId == seed.AttemptId);

            Assert.Equal(
                GameProgressStatus.InProgress,
                progress.Status);

            Assert.Equal(1, verifier.Calls);
        }
        finally
        {
            await CleanupAsync(seed);
        }
    }

    [M5SqlIntegrationFact]
    public async Task DuplicateSubmission_ShouldNotConsumeAgain()
    {
        var seed = await SeedAsync();

        try
        {
            var verifier = new TestCaptureVerifier();
            var command = Command(seed);

            await using (var db = CreateContext())
            {
                var repository =
                    new SqlGameProofSubmissionRepository(
                        db, verifier);

                await repository.SubmitCurrentStageProofAsync(
                    command,
                    seed.PlayerUserId,
                    ProofTime(seed));
            }

            await using (var db = CreateContext())
            {
                var repository =
                    new SqlGameProofSubmissionRepository(
                        db, verifier);

                await Assert.ThrowsAsync<
                    InvalidOperationException>(
                    () => repository
                        .SubmitCurrentStageProofAsync(
                            command,
                            seed.PlayerUserId,
                            ProofTime(seed)));
            }

            await using var verifyDb = CreateContext();

            Assert.Equal(
                1,
                await verifyDb.GameProofEvidence.CountAsync(
                    x => x.GameId == seed.GameId));

            Assert.Equal(1, verifier.Calls);
        }
        finally
        {
            await CleanupAsync(seed);
        }
    }

    [M5SqlIntegrationFact]
    public async Task WrongPlayer_ShouldNotPersistOrConsume()
    {
        var seed = await SeedAsync();

        try
        {
            var verifier = new TestCaptureVerifier();

            await using var db = CreateContext();

            var repository =
                new SqlGameProofSubmissionRepository(
                    db, verifier);

            await Assert.ThrowsAsync<
                UnauthorizedAccessException>(
                () => repository.SubmitCurrentStageProofAsync(
                    Command(seed),
                    "different-authenticated-player",
                    ProofTime(seed)));

            Assert.Equal(0, verifier.Calls);

            Assert.False(
                await db.GameProofEvidence.AnyAsync(
                    x => x.GameId == seed.GameId));
        }
        finally
        {
            await CleanupAsync(seed);
        }
    }

    [M5SqlIntegrationFact]
    public async Task ExpiredAttempt_ShouldRejectBeforeConsume()
    {
        var seed = await SeedAsync();

        try
        {
            var verifier = new TestCaptureVerifier();

            await using var db = CreateContext();

            var repository =
                new SqlGameProofSubmissionRepository(
                    db, verifier);

            // Deadline is exclusive.
            await Assert.ThrowsAsync<
                InvalidOperationException>(
                () => repository.SubmitCurrentStageProofAsync(
                    Command(seed),
                    seed.PlayerUserId,
                    seed.DeadlineUtc));

            Assert.Equal(0, verifier.Calls);

            Assert.False(
                await db.GameProofEvidence.AnyAsync(
                    x => x.GameId == seed.GameId));
        }
        finally
        {
            await CleanupAsync(seed);
        }
    }

    [M5SqlIntegrationFact]
    public async Task ExistingPendingProof_ShouldFailClosed()
    {
        var seed = await SeedAsync();

        try
        {
            // Simulate an existing unreviewed proof while
            // the progress is still marked InProgress.
            await using (var db = CreateContext())
            {
                db.GameProofEvidence.Add(
                    new GameProofEvidenceRow
                    {
                        Id = Guid.NewGuid(),
                        GameId = seed.GameId,
                        AttemptId = seed.AttemptId,
                        StepNumber = 1,
                        MediaId =
                            $"existing-{Guid.NewGuid():N}",
                        SubmittedAtUtc =
                            seed.StartedAtUtc.AddSeconds(5)
                    });

                await db.SaveChangesAsync();
            }

            var verifier = new TestCaptureVerifier();

            await using (var db = CreateContext())
            {
                var repository =
                    new SqlGameProofSubmissionRepository(
                        db, verifier);

                await Assert.ThrowsAsync<
                    InvalidOperationException>(
                    () => repository
                        .SubmitCurrentStageProofAsync(
                            Command(seed),
                            seed.PlayerUserId,
                            ProofTime(seed)));
            }

            await using var verifyDb = CreateContext();

            Assert.Equal(
                1,
                await verifyDb.GameProofEvidence.CountAsync(
                    x => x.GameId == seed.GameId));

            Assert.Equal(0, verifier.Calls);
        }
        finally
        {
            await CleanupAsync(seed);
        }
    }

    [M5SqlIntegrationFact]
    public async Task ConcurrentSubmissions_ShouldAcceptOnlyOne()
    {
        var seed = await SeedAsync();

        try
        {
            var verifier = new TestCaptureVerifier();

            var startGate = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

            async Task<bool> TrySubmitAsync(
                SubmitGameStageProofCommand command)
            {
                await using var db = CreateContext();

                var repository =
                    new SqlGameProofSubmissionRepository(
                        db, verifier);

                await startGate.Task;

                try
                {
                    await repository.SubmitCurrentStageProofAsync(
                        command,
                        seed.PlayerUserId,
                        ProofTime(seed));

                    return true;
                }
                catch (InvalidOperationException)
                {
                    return false;
                }
            }

            var first = TrySubmitAsync(Command(seed));
            var second = TrySubmitAsync(Command(seed));

            startGate.SetResult();

            var results = await Task.WhenAll(
                first, second);

            Assert.Equal(1, results.Count(x => x));
            Assert.Equal(1, results.Count(x => !x));

            await using var verifyDb = CreateContext();

            Assert.Equal(
                1,
                await verifyDb.GameProofEvidence.CountAsync(
                    x => x.GameId == seed.GameId));

            var progress = await verifyDb.GameAttemptProgress
                .AsNoTracking()
                .SingleAsync(x =>
                    x.AttemptId == seed.AttemptId);

            Assert.Equal(
                GameProgressStatus.WaitingForVerification,
                progress.Status);

            Assert.Equal(1, verifier.Calls);
        }
        finally
        {
            await CleanupAsync(seed);
        }
    }
    [M5SqlIntegrationFact]
    public async Task VerifierException_ShouldRollbackSqlChanges()
    {
        var seed = await SeedAsync();

        try
        {
            await using (var db = CreateContext())
            {
                var repository =
                    new SqlGameProofSubmissionRepository(
                        db,
                        new ThrowingCaptureVerifier());

                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => repository.SubmitCurrentStageProofAsync(
                        Command(seed),
                        seed.PlayerUserId,
                        ProofTime(seed)));
            }

            await using var verifyDb = CreateContext();

            Assert.Equal(
                0,
                await verifyDb.GameProofEvidence.CountAsync(
                    x => x.GameId == seed.GameId));

            var progress = await verifyDb.GameAttemptProgress
                .AsNoTracking()
                .SingleAsync(x =>
                    x.AttemptId == seed.AttemptId);

            Assert.Equal(
                GameProgressStatus.InProgress,
                progress.Status);
        }
        finally
        {
            await CleanupAsync(seed);
        }
    }
}
