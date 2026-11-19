
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

public sealed class M5SqlGameProofReviewRepositoryTests
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

    private sealed class TestAuthorizer
        : IGameStageProofDecisionAuthorizer
    {
        private readonly string _reviewerId;
        private readonly Guid _gameId;
        private readonly bool _allow;

        public TestAuthorizer(
            string reviewerId,
            Guid gameId,
            bool allow = true)
        {
            _reviewerId = reviewerId;
            _gameId = gameId;
            _allow = allow;
        }

        public Task<bool> IsAuthorizedAsync(
            string authenticatedReviewerUserId,
            Guid gameId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(
                _allow &&
                authenticatedReviewerUserId == _reviewerId &&
                gameId == _gameId);
        }
    }

    private sealed record TestSeed(
        Guid GameId,
        Guid AttemptId,
        Guid EvidenceId,
        string CreatorUserId,
        string PlayerUserId,
        string ReviewerUserId,
        DateTimeOffset ReviewedAtUtc);

    private static async Task<TestSeed> SeedPendingProofAsync()
    {
        // Requires the existing approved shared schema.
        // Never creates or applies migrations.

        var approvedAtUtc =
            DateTimeOffset.UtcNow.AddMinutes(-2);

        var submittedAtUtc =
            approvedAtUtc.AddMinutes(-1);

        var creator = UserAccount.RegisterPending(
            "M5 Proof Creator",
            $"m5-proof-creator-{Guid.NewGuid():N}@example.invalid",
            "test-only-password-hash-placeholder",
            submittedAtUtc);

        creator.ActivateAfterVerifiedOtp(submittedAtUtc);

        var player = UserAccount.RegisterPending(
            "M5 Proof Player",
            $"m5-proof-player-{Guid.NewGuid():N}@example.invalid",
            "test-only-password-hash-placeholder",
            submittedAtUtc);

        player.ActivateAfterVerifiedOtp(submittedAtUtc);

        var reviewer = UserAccount.RegisterPending(
            "M5 Proof Reviewer",
            $"m5-proof-reviewer-{Guid.NewGuid():N}@example.invalid",
            "test-only-password-hash-placeholder",
            submittedAtUtc);

        reviewer.ActivateAfterVerifiedOtp(submittedAtUtc);

        var game = Game.CreateDraft(
            Guid.NewGuid(),
            creator.UserId,
            "SQL Proof Review Test Game",
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
                    "Find checkpoint one",
                    null),
                new GameHint(
                    Guid.NewGuid(),
                    step2.Id,
                    "Find checkpoint two",
                    null)
            });

        game.SubmitForAdminReview(
            creator.UserId,
            submittedAtUtc);

        game.ApproveCreation(approvedAtUtc);

        await using var db = CreateContext();

        db.Users.AddRange(creator, player, reviewer);
        await db.SaveChangesAsync();

        await new SqlGameRepository(db).AddAsync(game);

        var startedAtUtc =
            approvedAtUtc.AddMinutes(1);

        var attempt =
            await new SqlGameAttemptRepository(db)
                .StartOrResumeAsync(
                    game.Id,
                    player.UserId,
                    startedAtUtc);

        var evidenceId = Guid.NewGuid();

        db.GameProofEvidence.Add(
            new GameProofEvidenceRow
            {
                Id = evidenceId,
                GameId = game.Id,
                AttemptId = attempt.AttemptId,
                StepNumber = 1,
                MediaId = $"proof-{Guid.NewGuid():N}",
                SubmittedAtUtc =
                    startedAtUtc.AddSeconds(10)
            });

        await db.SaveChangesAsync();

        var updated = await db.GameAttemptProgress
            .Where(x =>
                x.AttemptId == attempt.AttemptId &&
                x.Status == GameProgressStatus.InProgress)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    x => x.Status,
                    GameProgressStatus.WaitingForVerification));

        Assert.Equal(1, updated);

        return new TestSeed(
            game.Id,
            attempt.AttemptId,
            evidenceId,
            creator.UserId,
            player.UserId,
            reviewer.UserId,
            startedAtUtc.AddSeconds(20));
    }

    private static async Task CleanupAsync(TestSeed seed)
    {
        await using var db = CreateContext();

        // Delete review audits before their evidence.
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
                x.UserId == seed.PlayerUserId ||
                x.UserId == seed.ReviewerUserId)
            .ExecuteDeleteAsync();
    }

    private static GameStageProofDecisionCommand Command(
        TestSeed seed,
        bool approved,
        string? reason = null,
        Guid? decisionId = null)
    {
        return new GameStageProofDecisionCommand(
            DecisionId: decisionId ?? Guid.NewGuid(),
            GameId: seed.GameId,
            AttemptId: seed.AttemptId,
            EvidenceId: seed.EvidenceId,
            StepNumber: 1,
            Approved: approved,
            Reason: reason);
    }

    [M5SqlIntegrationFact]
    public async Task Approve_ShouldAdvanceStageAndPersistAudit()
    {
        var seed = await SeedPendingProofAsync();

        try
        {
            var command = Command(seed, approved: true);

            await using (var db = CreateContext())
            {
                var repo = new SqlGameProofReviewRepository(
                    db,
                    new TestAuthorizer(
                        seed.ReviewerUserId,
                        seed.GameId));

                var result = await repo.ApplyDecisionAsync(
                    command,
                    seed.ReviewerUserId,
                    seed.ReviewedAtUtc);

                Assert.True(result.Approved);
                Assert.Equal(2, result.CurrentStepNumber);
                Assert.Equal(
                    GameProgressStatus.InProgress,
                    result.ProgressStatus);
                Assert.Equal(0, result.RejectedEvidenceCount);
            }

            await using var verifyDb = CreateContext();

            var progress = await verifyDb.GameAttemptProgress
                .AsNoTracking()
                .SingleAsync(x => x.AttemptId == seed.AttemptId);

            Assert.Equal(2, progress.CurrentStepNumber);
            Assert.Equal(
                GameProgressStatus.InProgress,
                progress.Status);

            var audit = await verifyDb.GameProofReviewDecisions
                .AsNoTracking()
                .SingleAsync(x => x.DecisionId == command.DecisionId);

            Assert.Equal(seed.EvidenceId, audit.EvidenceId);
            Assert.True(audit.IsApproved);
            Assert.Equal(seed.ReviewerUserId, audit.ReviewerUserId);
        }
        finally
        {
            await CleanupAsync(seed);
        }
    }

    [M5SqlIntegrationFact]
    public async Task Reject_ShouldKeepStageAndIncrementCount()
    {
        var seed = await SeedPendingProofAsync();

        try
        {
            var command = Command(
                seed,
                approved: false,
                reason: "Proof is unclear");

            await using (var db = CreateContext())
            {
                var repo = new SqlGameProofReviewRepository(
                    db,
                    new TestAuthorizer(
                        seed.ReviewerUserId,
                        seed.GameId));

                var result = await repo.ApplyDecisionAsync(
                    command,
                    seed.ReviewerUserId,
                    seed.ReviewedAtUtc);

                Assert.False(result.Approved);
                Assert.Equal(1, result.CurrentStepNumber);
                Assert.Equal(1, result.RejectedEvidenceCount);
                Assert.Equal(
                    GameProgressStatus.InProgress,
                    result.ProgressStatus);
            }

            await using var verifyDb = CreateContext();

            var progress = await verifyDb.GameAttemptProgress
                .AsNoTracking()
                .SingleAsync(x => x.AttemptId == seed.AttemptId);

            Assert.Equal(1, progress.CurrentStepNumber);
            Assert.Equal(1, progress.RejectedEvidenceCount);

            Assert.Single(
                await verifyDb.GameProofReviewDecisions
                    .Where(x => x.EvidenceId == seed.EvidenceId)
                    .ToListAsync());
        }
        finally
        {
            await CleanupAsync(seed);
        }
    }

    [M5SqlIntegrationFact]
    public async Task UnauthorizedReview_ShouldNotChangeSql()
    {
        var seed = await SeedPendingProofAsync();

        try
        {
            await using (var db = CreateContext())
            {
                var repo = new SqlGameProofReviewRepository(
                    db,
                    new TestAuthorizer(
                        seed.ReviewerUserId,
                        seed.GameId,
                        allow: false));

                await Assert.ThrowsAsync<UnauthorizedAccessException>(
                    () => repo.ApplyDecisionAsync(
                        Command(seed, approved: true),
                        seed.ReviewerUserId,
                        seed.ReviewedAtUtc));
            }

            await using var verifyDb = CreateContext();

            var progress = await verifyDb.GameAttemptProgress
                .AsNoTracking()
                .SingleAsync(x => x.AttemptId == seed.AttemptId);

            Assert.Equal(
                GameProgressStatus.WaitingForVerification,
                progress.Status);

            Assert.Equal(
                0,
                await verifyDb.GameProofReviewDecisions
                    .CountAsync(x =>
                        x.EvidenceId == seed.EvidenceId));
        }
        finally
        {
            await CleanupAsync(seed);
        }
    }

    [M5SqlIntegrationFact]
    public async Task IdenticalRetry_ShouldNotApplyDecisionTwice()
    {
        var seed = await SeedPendingProofAsync();

        try
        {
            var command = Command(
                seed,
                approved: false,
                reason: "Retake required");

            GameStageProofDecisionResultDto first;

            await using (var db = CreateContext())
            {
                var repo = new SqlGameProofReviewRepository(
                    db,
                    new TestAuthorizer(
                        seed.ReviewerUserId,
                        seed.GameId));

                first = await repo.ApplyDecisionAsync(
                    command,
                    seed.ReviewerUserId,
                    seed.ReviewedAtUtc);
            }

            await using (var db = CreateContext())
            {
                var repo = new SqlGameProofReviewRepository(
                    db,
                    new TestAuthorizer(
                        seed.ReviewerUserId,
                        seed.GameId));

                var retry = await repo.ApplyDecisionAsync(
                    command,
                    seed.ReviewerUserId,
                    seed.ReviewedAtUtc);

                Assert.Equal(first, retry);
            }

            await using var verifyDb = CreateContext();

            var progress = await verifyDb.GameAttemptProgress
                .AsNoTracking()
                .SingleAsync(x => x.AttemptId == seed.AttemptId);

            Assert.Equal(1, progress.RejectedEvidenceCount);

            Assert.Equal(
                1,
                await verifyDb.GameProofReviewDecisions
                    .CountAsync(x =>
                        x.EvidenceId == seed.EvidenceId));
        }
        finally
        {
            await CleanupAsync(seed);
        }
    }

    [M5SqlIntegrationFact]
    public async Task ThreeRejections_ShouldCloseAttempt_AndPreserveHistoricalRetry()
    {
        var seed = await SeedPendingProofAsync();

        try
        {
            Guid evidenceId = seed.EvidenceId;

            GameStageProofDecisionCommand? firstCommand = null;
            GameStageProofDecisionResultDto? firstResult = null;

            for (int round = 1; round <= 3; round++)
            {
                var reviewedAtUtc =
                    seed.ReviewedAtUtc.AddSeconds(
                        (round - 1) * 15);

                // Each retake must have a separate
                // evidence record.
                if (round > 1)
                {
                    evidenceId = Guid.NewGuid();

                    await using var prepareDb = CreateContext();

                    prepareDb.GameProofEvidence.Add(
                        new GameProofEvidenceRow
                        {
                            Id = evidenceId,
                            GameId = seed.GameId,
                            AttemptId = seed.AttemptId,
                            StepNumber = 1,
                            MediaId = $"retake-{Guid.NewGuid():N}",
                            SubmittedAtUtc =
                                reviewedAtUtc.AddSeconds(-5)
                        });

                    await prepareDb.SaveChangesAsync();

                    int updated =
                        await prepareDb.GameAttemptProgress
                            .Where(x =>
                                x.AttemptId == seed.AttemptId &&
                                x.Status ==
                                    GameProgressStatus.InProgress &&
                                x.RejectedEvidenceCount ==
                                    round - 1)
                            .ExecuteUpdateAsync(
                                setters => setters.SetProperty(
                                    x => x.Status,
                                    GameProgressStatus
                                        .WaitingForVerification));

                    Assert.Equal(1, updated);
                }

                var currentSeed =
                    seed with { EvidenceId = evidenceId };

                var command = Command(
                    currentSeed,
                    approved: false,
                    reason: "Retake required");

                await using var db = CreateContext();

                var repo = new SqlGameProofReviewRepository(
                    db,
                    new TestAuthorizer(
                        seed.ReviewerUserId,
                        seed.GameId));

                var result = await repo.ApplyDecisionAsync(
                    command,
                    seed.ReviewerUserId,
                    reviewedAtUtc);

                Assert.Equal(
                    round,
                    result.RejectedEvidenceCount);

                Assert.Equal(1, result.CurrentStepNumber);

                Assert.Equal(
                    round == 3
                        ? GameProgressStatus.Rejected
                        : GameProgressStatus.InProgress,
                    result.ProgressStatus);

                if (round == 1)
                {
                    firstCommand = command;
                    firstResult = result;
                }
            }

            await using (var verifyDb = CreateContext())
            {
                var progress =
                    await verifyDb.GameAttemptProgress
                        .AsNoTracking()
                        .SingleAsync(
                            x => x.AttemptId == seed.AttemptId);

                Assert.Equal(3, progress.RejectedEvidenceCount);
                Assert.Equal(
                    GameProgressStatus.Rejected,
                    progress.Status);

                Assert.Equal(
                    3,
                    await verifyDb.GameProofReviewDecisions
                        .CountAsync(x =>
                            verifyDb.GameProofEvidence.Any(
                                e => e.Id == x.EvidenceId &&
                                     e.GameId == seed.GameId)));
            }

            // Historical retry must return the ORIGINAL
            // decision result, not the current terminal state.
            Assert.NotNull(firstCommand);
            Assert.NotNull(firstResult);

            await using (var retryDb = CreateContext())
            {
                var repo = new SqlGameProofReviewRepository(
                    retryDb,
                    new TestAuthorizer(
                        seed.ReviewerUserId,
                        seed.GameId));

                var retry = await repo.ApplyDecisionAsync(
                    firstCommand!,
                    seed.ReviewerUserId,
                    seed.ReviewedAtUtc);

                Assert.Equal(firstResult, retry);
                Assert.Equal(
                    1,
                    retry.RejectedEvidenceCount);
            }
        }
        finally
        {
            await CleanupAsync(seed);
        }
    }

    [M5SqlIntegrationFact]
    public async Task ConcurrentReviews_ShouldAcceptExactlyOneDecision()
    {
        var seed = await SeedPendingProofAsync();

        try
        {
            var startGate = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            var commands = new[]
            {
                Command(seed, approved: true),
                Command(seed, approved: true)
            };

            var tasks = commands.Select(async command =>
            {
                await using var db = CreateContext();

                var repo = new SqlGameProofReviewRepository(
                    db,
                    new TestAuthorizer(
                        seed.ReviewerUserId,
                        seed.GameId));

                await startGate.Task;

                try
                {
                    await repo.ApplyDecisionAsync(
                        command,
                        seed.ReviewerUserId,
                        seed.ReviewedAtUtc);

                    return (Success: true, Error: (Exception?)null);
                }
                catch (Exception ex)
                {
                    return (Success: false, Error: (Exception?)ex);
                }
            }).ToArray();

            startGate.SetResult(true);

            var results = await Task.WhenAll(tasks);

            Assert.Equal(
                1,
                results.Count(x => x.Success));

            var failed = Assert.Single(
                results, x => !x.Success);

            Assert.IsType<InvalidOperationException>(
                failed.Error);

            await using var verifyDb = CreateContext();

            Assert.Equal(
                1,
                await verifyDb.GameProofReviewDecisions
                    .CountAsync(x =>
                        x.EvidenceId == seed.EvidenceId));

            var progress = await verifyDb.GameAttemptProgress
                .AsNoTracking()
                .SingleAsync(
                    x => x.AttemptId == seed.AttemptId);

            Assert.Equal(2, progress.CurrentStepNumber);
            Assert.Equal(
                GameProgressStatus.InProgress,
                progress.Status);
        }
        finally
        {
            await CleanupAsync(seed);
        }
    }

    [M5SqlIntegrationFact]
    public async Task DecisionRetry_WithChangedReviewTime_ShouldBeRejected()
    {
        var seed = await SeedPendingProofAsync();

        try
        {
            var command = Command(seed, approved: true);

            await using (var db = CreateContext())
            {
                var repo = new SqlGameProofReviewRepository(
                    db,
                    new TestAuthorizer(
                        seed.ReviewerUserId,
                        seed.GameId));

                await repo.ApplyDecisionAsync(
                    command,
                    seed.ReviewerUserId,
                    seed.ReviewedAtUtc);
            }

            // Same DecisionId but a different review timestamp.
            // This is NOT an identical retry.
            await using (var db = CreateContext())
            {
                var repo = new SqlGameProofReviewRepository(
                    db,
                    new TestAuthorizer(
                        seed.ReviewerUserId,
                        seed.GameId));

                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => repo.ApplyDecisionAsync(
                        command,
                        seed.ReviewerUserId,
                        seed.ReviewedAtUtc.AddSeconds(1)));
            }

            await using var verifyDb = CreateContext();

            Assert.Equal(
                1,
                await verifyDb.GameProofReviewDecisions
                    .CountAsync(x =>
                        x.EvidenceId == seed.EvidenceId));

            var progress = await verifyDb.GameAttemptProgress
                .AsNoTracking()
                .SingleAsync(
                    x => x.AttemptId == seed.AttemptId);

            Assert.Equal(2, progress.CurrentStepNumber);
            Assert.Equal(
                GameProgressStatus.InProgress,
                progress.Status);
        }
        finally
        {
            await CleanupAsync(seed);
        }
    }

    [M5SqlIntegrationFact]
    public async Task FinalStageApproval_ShouldFailClosedWithoutCompletion()
    {
        var seed = await SeedPendingProofAsync();

        try
        {
            var finalEvidenceId = Guid.NewGuid();

            await using (var prepareDb = CreateContext())
            {
                prepareDb.GameProofEvidence.Add(
                    new GameProofEvidenceRow
                    {
                        Id = finalEvidenceId,
                        GameId = seed.GameId,
                        AttemptId = seed.AttemptId,
                        StepNumber = 2,
                        MediaId = $"final-{Guid.NewGuid():N}",
                        SubmittedAtUtc =
                            seed.ReviewedAtUtc.AddSeconds(-1)
                    });

                await prepareDb.SaveChangesAsync();

                // Simulate the final stage awaiting review.
                int updated = await prepareDb.GameAttemptProgress
                    .Where(x =>
                        x.AttemptId == seed.AttemptId &&
                        x.Status ==
                            GameProgressStatus.WaitingForVerification)
                    .ExecuteUpdateAsync(
                        setters => setters
                            .SetProperty(
                                x => x.CurrentStepNumber, 2));

                Assert.Equal(1, updated);
            }

            var command = new GameStageProofDecisionCommand(
                DecisionId: Guid.NewGuid(),
                GameId: seed.GameId,
                AttemptId: seed.AttemptId,
                EvidenceId: finalEvidenceId,
                StepNumber: 2,
                Approved: true,
                Reason: null);

            await using (var db = CreateContext())
            {
                var repo = new SqlGameProofReviewRepository(
                    db,
                    new TestAuthorizer(
                        seed.ReviewerUserId,
                        seed.GameId));

                var error =
                    await Assert.ThrowsAsync<InvalidOperationException>(
                        () => repo.ApplyDecisionAsync(
                            command,
                            seed.ReviewerUserId,
                            seed.ReviewedAtUtc));

                Assert.Contains(
                    "dedicated SQL completion workflow",
                    error.Message);
            }

            await using var verifyDb = CreateContext();

            var progress = await verifyDb.GameAttemptProgress
                .AsNoTracking()
                .SingleAsync(
                    x => x.AttemptId == seed.AttemptId);

            Assert.Equal(2, progress.CurrentStepNumber);

            Assert.Equal(
                GameProgressStatus.WaitingForVerification,
                progress.Status);

            // No review decision should be committed.
            Assert.Equal(
                0,
                await verifyDb.GameProofReviewDecisions
                    .CountAsync(x =>
                        x.EvidenceId == finalEvidenceId));

            // No incomplete completion result should appear.
            Assert.Equal(
                0,
                await verifyDb.GameCompletionResults
                    .CountAsync(x =>
                        x.AttemptId == seed.AttemptId));

            Assert.Equal(
                0,
                await verifyDb.GameWinners
                    .CountAsync(x =>
                        x.GameId == seed.GameId));
        }
        finally
        {
            await CleanupAsync(seed);
        }
    }

    [M5SqlIntegrationFact]
    public async Task HistoricalRetry_ShouldPreserveOriginalCount_WhenEvidenceTimesAreOutOfOrder()
    {
        var seed = await SeedPendingProofAsync();

        try
        {
            var firstCommand = Command(
                seed,
                approved: false,
                reason: "First rejection");

            GameStageProofDecisionResultDto firstResult;

            // First review: rejection count becomes 1.
            await using (var db = CreateContext())
            {
                var repo = new SqlGameProofReviewRepository(
                    db,
                    new TestAuthorizer(
                        seed.ReviewerUserId,
                        seed.GameId));

                firstResult = await repo.ApplyDecisionAsync(
                    firstCommand,
                    seed.ReviewerUserId,
                    seed.ReviewedAtUtc);
            }

            Assert.Equal(1, firstResult.RejectedEvidenceCount);

            // Simulate an evidence record with an earlier
            // submission timestamp, reviewed later.
            var secondEvidenceId = Guid.NewGuid();

            await using (var prepareDb = CreateContext())
            {
                prepareDb.GameProofEvidence.Add(
                    new GameProofEvidenceRow
                    {
                        Id = secondEvidenceId,
                        GameId = seed.GameId,
                        AttemptId = seed.AttemptId,
                        StepNumber = 1,
                        MediaId = $"out-of-order-{Guid.NewGuid():N}",
                        SubmittedAtUtc =
                            seed.ReviewedAtUtc.AddSeconds(-15)
                    });

                await prepareDb.SaveChangesAsync();

                var updated = await prepareDb.GameAttemptProgress
                    .Where(x =>
                        x.AttemptId == seed.AttemptId &&
                        x.Status == GameProgressStatus.InProgress &&
                        x.RejectedEvidenceCount == 1)
                    .ExecuteUpdateAsync(
                        setters => setters.SetProperty(
                            x => x.Status,
                            GameProgressStatus.WaitingForVerification));

                Assert.Equal(1, updated);
            }

            var secondSeed = seed with
            {
                EvidenceId = secondEvidenceId
            };

            var secondCommand = Command(
                secondSeed,
                approved: false,
                reason: "Second rejection");

            // Second review: rejection count becomes 2.
            await using (var db = CreateContext())
            {
                var repo = new SqlGameProofReviewRepository(
                    db,
                    new TestAuthorizer(
                        seed.ReviewerUserId,
                        seed.GameId));

                var secondResult = await repo.ApplyDecisionAsync(
                    secondCommand,
                    seed.ReviewerUserId,
                    seed.ReviewedAtUtc.AddSeconds(5));

                Assert.Equal(
                    2,
                    secondResult.RejectedEvidenceCount);
            }

            // Retry the FIRST review. It must return its
            // original result: rejection count 1, not 2.
            await using (var db = CreateContext())
            {
                var repo = new SqlGameProofReviewRepository(
                    db,
                    new TestAuthorizer(
                        seed.ReviewerUserId,
                        seed.GameId));

                var retry = await repo.ApplyDecisionAsync(
                    firstCommand,
                    seed.ReviewerUserId,
                    seed.ReviewedAtUtc);

                Assert.Equal(firstResult, retry);
                Assert.Equal(1, retry.RejectedEvidenceCount);
            }

            await using var verifyDb = CreateContext();

            var progress = await verifyDb.GameAttemptProgress
                .AsNoTracking()
                .SingleAsync(
                    x => x.AttemptId == seed.AttemptId);

            Assert.Equal(2, progress.RejectedEvidenceCount);
            Assert.Equal(
                GameProgressStatus.InProgress,
                progress.Status);

            Assert.Equal(
                2,
                await verifyDb.GameProofReviewDecisions
                    .CountAsync(x =>
                        verifyDb.GameProofEvidence.Any(e =>
                            e.Id == x.EvidenceId &&
                            e.AttemptId == seed.AttemptId)));
        }
        finally
        {
            await CleanupAsync(seed);
        }
    }

    [M5SqlIntegrationFact]
    public async Task MultiplePendingProofs_ShouldFailClosedWithoutApproval()
    {
        var seed = await SeedPendingProofAsync();

        try
        {
            // The seed already has one unreviewed proof.
            // Add another unreviewed proof for the same stage.
            await using (var prepareDb = CreateContext())
            {
                prepareDb.GameProofEvidence.Add(
                    new GameProofEvidenceRow
                    {
                        Id = Guid.NewGuid(),
                        GameId = seed.GameId,
                        AttemptId = seed.AttemptId,
                        StepNumber = 1,
                        MediaId =
                            $"duplicate-pending-{Guid.NewGuid():N}",
                        SubmittedAtUtc =
                            seed.ReviewedAtUtc.AddSeconds(-5)
                    });

                await prepareDb.SaveChangesAsync();
            }

            // Ambiguous pending evidence must not be approved.
            await using (var db = CreateContext())
            {
                var repo = new SqlGameProofReviewRepository(
                    db,
                    new TestAuthorizer(
                        seed.ReviewerUserId,
                        seed.GameId));

                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => repo.ApplyDecisionAsync(
                        Command(seed, approved: true),
                        seed.ReviewerUserId,
                        seed.ReviewedAtUtc));
            }

            await using var verifyDb = CreateContext();

            var progress = await verifyDb.GameAttemptProgress
                .AsNoTracking()
                .SingleAsync(
                    x => x.AttemptId == seed.AttemptId);

            Assert.Equal(1, progress.CurrentStepNumber);

            Assert.Equal(
                GameProgressStatus.WaitingForVerification,
                progress.Status);

            Assert.Equal(0, progress.RejectedEvidenceCount);

            // No review decision should be persisted.
            Assert.Equal(
                0,
                await verifyDb.GameProofReviewDecisions
                    .CountAsync(x =>
                        verifyDb.GameProofEvidence.Any(e =>
                            e.Id == x.EvidenceId &&
                            e.AttemptId == seed.AttemptId)));
        }
        finally
        {
            await CleanupAsync(seed);
        }
    }

    [M5SqlIntegrationFact]
    public async Task PreviouslyRejectedProof_WithNewDecisionId_ShouldNotBeApproved()
    {
        var seed = await SeedPendingProofAsync();

        try
        {
            // First decision: reject Evidence A.
            var firstCommand = Command(
                seed,
                approved: false,
                reason: "Retake required");

            await using (var db = CreateContext())
            {
                var repo = new SqlGameProofReviewRepository(
                    db,
                    new TestAuthorizer(
                        seed.ReviewerUserId,
                        seed.GameId));

                var result = await repo.ApplyDecisionAsync(
                    firstCommand,
                    seed.ReviewerUserId,
                    seed.ReviewedAtUtc);

                Assert.Equal(1, result.RejectedEvidenceCount);
            }

            // Player submits Evidence B for the same stage.
            var newEvidenceId = Guid.NewGuid();

            await using (var db = CreateContext())
            {
                db.GameProofEvidence.Add(
                    new GameProofEvidenceRow
                    {
                        Id = newEvidenceId,
                        GameId = seed.GameId,
                        AttemptId = seed.AttemptId,
                        StepNumber = 1,
                        MediaId = $"retake-{Guid.NewGuid():N}",
                        SubmittedAtUtc =
                            seed.ReviewedAtUtc.AddSeconds(2)
                    });

                await db.SaveChangesAsync();

                var updated = await db.GameAttemptProgress
                    .Where(x =>
                        x.AttemptId == seed.AttemptId &&
                        x.Status == GameProgressStatus.InProgress &&
                        x.RejectedEvidenceCount == 1)
                    .ExecuteUpdateAsync(
                        setters => setters.SetProperty(
                            x => x.Status,
                            GameProgressStatus.WaitingForVerification));

                Assert.Equal(1, updated);
            }

            // Attack: use a NEW DecisionId to approve
            // the already rejected Evidence A.
            var conflictingCommand = Command(
                seed,
                approved: true);

            await using (var db = CreateContext())
            {
                var repo = new SqlGameProofReviewRepository(
                    db,
                    new TestAuthorizer(
                        seed.ReviewerUserId,
                        seed.GameId));

                var error =
                    await Assert.ThrowsAsync<InvalidOperationException>(
                        () => repo.ApplyDecisionAsync(
                            conflictingCommand,
                            seed.ReviewerUserId,
                            seed.ReviewedAtUtc.AddSeconds(5)));

                Assert.Contains(
                    "exactly one pending proof",
                    error.Message);
            }

            // Verify SQL state was not changed.
            await using var verifyDb = CreateContext();

            var progress = await verifyDb.GameAttemptProgress
                .AsNoTracking()
                .SingleAsync(x =>
                    x.AttemptId == seed.AttemptId);

            Assert.Equal(1, progress.CurrentStepNumber);
            Assert.Equal(1, progress.RejectedEvidenceCount);

            Assert.Equal(
                GameProgressStatus.WaitingForVerification,
                progress.Status);

            // Only the original rejection audit exists.
            Assert.Equal(
                1,
                await verifyDb.GameProofReviewDecisions
                    .CountAsync(x =>
                        x.EvidenceId == seed.EvidenceId));

            Assert.Equal(
                0,
                await verifyDb.GameProofReviewDecisions
                    .CountAsync(x =>
                        x.DecisionId ==
                            conflictingCommand.DecisionId));

            // Evidence B remains unreviewed.
            Assert.Equal(
                0,
                await verifyDb.GameProofReviewDecisions
                    .CountAsync(x =>
                        x.EvidenceId == newEvidenceId));
        }
        finally
        {
            await CleanupAsync(seed);
        }
    }

}
