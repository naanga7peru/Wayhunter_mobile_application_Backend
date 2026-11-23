
using System;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

using Wayhunter.Domain.Modules.HunterPlaces.Entities;
using Wayhunter.Domain.Modules.HunterPlaces.Enums;
using Wayhunter.Domain.Modules.IdentitySocial.Entities;

using Wayhunter.Infrastructure.Persistence.DbContexts;
using Wayhunter.Infrastructure.Persistence.Models.HunterPlaces;
using Wayhunter.Infrastructure.Persistence.Repositories.HunterPlaces;
using Wayhunter.Infrastructure.Persistence.Configurations.IdentitySocial;
using Wayhunter.Infrastructure.Persistence.Configurations.HunterPlaces;

using Xunit;

namespace Wayhunter.IntegrationTests.Modules.HunterPlaces;

// =====================================================
// M4 SQL Integration Test Attribute
// =====================================================

public sealed class M4SqlIntegrationFactAttribute : FactAttribute
{
    public M4SqlIntegrationFactAttribute()
    {
        var connectionString =
            Environment.GetEnvironmentVariable(
                "WAYHUNTER_M4_TEST_SQL_CONNECTION");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Skip = "M4 isolated SQL test database is not configured.";
        }
    }
}

// =====================================================
// M4 SQL Integration Verification Tests
// =====================================================

public sealed class M4SqlIntegrationVerificationTests
{
    private const string AllowedDatabasePrefix = "Wayhunter_Test_";
    private const string AllowedServer = @"(localdb)\MSSQLLocalDB";

    private static readonly SemaphoreSlim SchemaGate = new(1, 1);

    private static bool _schemaInitialized;

    // =================================================
    // Validate Dedicated Test Database
    // =================================================

    private static string GetTestConnectionString()
    {
        var connectionString =
            Environment.GetEnvironmentVariable(
                "WAYHUNTER_M4_TEST_SQL_CONNECTION");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "M4 test database connection is missing.");
        }

        var builder =
            new SqlConnectionStringBuilder(connectionString);

        if (!builder.InitialCatalog.StartsWith(
                AllowedDatabasePrefix,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Only the dedicated M4 test database is allowed.");
        }

        if (!string.Equals(builder.DataSource, AllowedServer, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only the dedicated LocalDB instance is allowed.");

        return connectionString;
    }

    // =================================================
    // Shared Production DbContext Factory
    // =================================================

    private static WayhunterSharedDbContext CreateContext(
        DbCommandInterceptor? interceptor = null)
    {
        var optionsBuilder =
            new DbContextOptionsBuilder<WayhunterSharedDbContext>()
                .UseSqlServer(GetTestConnectionString());

        if (interceptor is not null)
        {
            optionsBuilder.AddInterceptors(interceptor);
        }

        return new WayhunterSharedDbContext(
            optionsBuilder.Options);
    }

    // =================================================
    // M4 Isolated Test Schema Context
    // =================================================

    // This context contains only M1 UserAccount and
    // M4 Hunter entities. It does not create other
    // module tables in the test database.

    private sealed class M4TestSchemaContext : DbContext
    {
        public M4TestSchemaContext(
            DbContextOptions<M4TestSchemaContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(
            ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.ApplyConfiguration(
                new UserAccountConfiguration());

            modelBuilder.ApplyConfiguration(
                new HunterSubmissionConfiguration());

            modelBuilder.ApplyConfiguration(
                new HunterEvidenceConfiguration());

            modelBuilder.ApplyConfiguration(
                new HunterReviewDecisionConfiguration());

            modelBuilder.ApplyConfiguration(
                new HunterCaptureSessionRowConfiguration());
        }
    }

    // =================================================
    // Prepare Empty Test Database Schema
    // =================================================

    private static async Task EnsureM4TestSchemaAsync()
    {
        await SchemaGate.WaitAsync();

        try
        {
            if (_schemaInitialized)
            {
                return;
            }

            var options =
                new DbContextOptionsBuilder<M4TestSchemaContext>()
                    .UseSqlServer(GetTestConnectionString())
                    .Options;

            await using var db =
                new M4TestSchemaContext(options);

            // Creates tables directly from this limited
            // test model. Does not generate or apply
            // EF Core migrations.
            await db.Database.EnsureCreatedAsync();

            _schemaInitialized = true;
        }
        finally
        {
            SchemaGate.Release();
        }
    }

    // =================================================
    // Test Data Seeder
    // =================================================

    private static async Task<(
        Guid HunterId,
        string UserId,
        Guid SubmissionId,
        Guid SessionId)> SeedAsync()
    {
        await EnsureM4TestSchemaAsync();

        var now = DateTimeOffset.UtcNow;

        var user = UserAccount.RegisterPending(
            "M4 SQL Test Hunter",
            $"m4-sql-{Guid.NewGuid():N}@example.invalid",
            "test-only-password-hash-placeholder",
            now);

        user.ActivateAfterVerifiedOtp(now);

        var submission = new HunterSubmission
        {
            Id = Guid.NewGuid(),

            UserId = user.UserId,

            UserNameSnapshot = "M4 SQL Test Hunter",

            PlaceName = "M4 SQL Test Place",

            Description = "M4 SQL integration test",

            District = "Jaffna",

            Latitude = 9.66845,

            Longitude = 80.0074,

            Status = HunterSubmissionStatus.Draft,

            CreatedAtUtc = now
        };

        var session = new HunterCaptureSessionRow
        {
            Id = Guid.NewGuid(),

            HunterId = Guid.Parse(user.UserId),

            HunterSubmissionId = submission.Id,

            IssuedAtUtc = now.AddMinutes(-1),

            ExpiresAtUtc = now.AddMinutes(10),

            IsConsumed = false,

            IsRevoked = false
        };

        await using var db = CreateContext();

        db.Users.Add(user);
        await db.SaveChangesAsync();

        db.HunterSubmissions.Add(submission);
        await db.SaveChangesAsync();

        db.HunterCaptureSessions.Add(session);

        await db.SaveChangesAsync();

        return (
            Guid.Parse(user.UserId),
            user.UserId,
            submission.Id,
            session.Id);
    }

    private static async Task MarkPendingReviewAsync(
        Guid submissionId,
        DateTimeOffset submittedAtUtc)
    {
        await using var db = CreateContext();
        var updated = await db.HunterSubmissions
            .Where(x => x.Id == submissionId &&
                        x.Status == HunterSubmissionStatus.Draft)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(
                    x => x.Status,
                    HunterSubmissionStatus.PendingReview)
                .SetProperty(x => x.SubmittedAtUtc, submittedAtUtc));

        if (updated != 1)
        {
            throw new InvalidOperationException(
                "Could not prepare the isolated pending-review fixture.");
        }
    }

    // =================================================
    // Cleanup Test Data
    // =================================================

    private static async Task CleanupAsync(
        Guid submissionId,
        string userId)
    {
        await using var db = CreateContext();

        await db.HunterCaptureSessions
            .Where(x => x.HunterSubmissionId == submissionId)
            .ExecuteDeleteAsync();

        await db.HunterReviewDecisions
            .Where(x => x.HunterSubmissionId == submissionId)
            .ExecuteDeleteAsync();

        await db.HunterEvidence
            .Where(x => x.HunterSubmissionId == submissionId)
            .ExecuteDeleteAsync();

        await db.HunterSubmissions
            .Where(x => x.Id == submissionId)
            .ExecuteDeleteAsync();

        await db.Users
            .Where(x => x.UserId == userId)
            .ExecuteDeleteAsync();
    }

    // =================================================
    // TEST 01 - Concurrent Session Consumption
    // =================================================

    [M4SqlIntegrationFact]
    public async Task ConcurrentConsume_ExactlyOne_ShouldSucceed()
    {
        var seed = await SeedAsync();

        try
        {
            async Task<bool> ConsumeAsync()
            {
                await using var db = CreateContext();

                var store =
                    new SqlHunterCaptureSessionStore(db);

                return await store.TryConsumeAsync(
                    seed.SessionId,
                    seed.HunterId,
                    seed.SubmissionId,
                    DateTimeOffset.UtcNow);
            }

            // Two independent requests.
            var results = await Task.WhenAll(
                ConsumeAsync(),
                ConsumeAsync());

            Assert.Single(results, result => result);

            await using var verifyDb = CreateContext();

            var consumed = await verifyDb.HunterCaptureSessions
                .AsNoTracking()
                .Where(x => x.Id == seed.SessionId)
                .Select(x => x.IsConsumed)
                .SingleAsync();

            Assert.True(consumed);
        }
        finally
        {
            await CleanupAsync(
                seed.SubmissionId,
                seed.UserId);
        }
    }

    // =================================================
    // TEST 02 - Status Transition and Revocation
    // =================================================

    [M4SqlIntegrationFact]
    public async Task StatusTransition_ShouldRevokeUnusedSessions()
    {
        var seed = await SeedAsync();
        var submittedAtUtc = DateTimeOffset.UtcNow;

        try
        {
            await using (var db = CreateContext())
            {
                var store =
                    new SqlHunterSubmissionStatusTransitionStore(db);

                var changed = await store
                    .TrySubmitDraftAndRevokeCaptureSessionsAsync(
                        seed.SubmissionId,
                        submittedAtUtc);

                Assert.True(changed);
            }

            await using var verifyDb = CreateContext();

            var status = await verifyDb.HunterSubmissions
                .AsNoTracking()
                .Where(x => x.Id == seed.SubmissionId)
                .Select(x => x.Status)
                .SingleAsync();
            var persistedSubmittedAtUtc = await verifyDb.HunterSubmissions
                .AsNoTracking()
                .Where(x => x.Id == seed.SubmissionId)
                .Select(x => x.SubmittedAtUtc)
                .SingleAsync();

            var revoked = await verifyDb.HunterCaptureSessions
                .AsNoTracking()
                .Where(x => x.Id == seed.SessionId)
                .Select(x => x.IsRevoked)
                .SingleAsync();

            Assert.Equal(
                HunterSubmissionStatus.PendingReview,
                status);
            Assert.Equal(submittedAtUtc, persistedSubmittedAtUtc);

            Assert.True(revoked);
        }
        finally
        {
            await CleanupAsync(
                seed.SubmissionId,
                seed.UserId);
        }
    }

    // =================================================
    // TEST 03 - Rollback on Revocation Failure
    // =================================================

    [M4SqlIntegrationFact]
    public async Task RevocationFailure_ShouldRollbackStatusTransition()
    {
        var seed = await SeedAsync();

        try
        {
            var interceptor =
                new FailHunterRevocationInterceptor();

            await using (var db = CreateContext(interceptor))
            {
                var store =
                    new SqlHunterSubmissionStatusTransitionStore(db);

                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => store.TrySubmitDraftAndRevokeCaptureSessionsAsync(
                        seed.SubmissionId,
                        DateTimeOffset.UtcNow));
            }

            Assert.True(interceptor.StatusUpdateObserved);

            Assert.True(interceptor.RevocationBlocked);

            // Verify using a fresh context.
            await using var verifyDb = CreateContext();

            var submission = await verifyDb.HunterSubmissions
                .AsNoTracking()
                .SingleAsync(x => x.Id == seed.SubmissionId);

            var session = await verifyDb.HunterCaptureSessions
                .AsNoTracking()
                .SingleAsync(x => x.Id == seed.SessionId);

            // The status update must be rolled back.
            Assert.Equal(
                HunterSubmissionStatus.Draft,
                submission.Status);

            // The session must remain unchanged.
            Assert.False(session.IsRevoked);

            Assert.False(session.IsConsumed);
        }
        finally
        {
            await CleanupAsync(
                seed.SubmissionId,
                seed.UserId);
        }
    }

    // =================================================
    // TEST 04 - Atomic Admin Review and Retry Protection
    // =================================================

    [M4SqlIntegrationFact]
    public async Task Review_ShouldCommitDecisionStatusAndSessionRevocationOnce()
    {
        var seed = await SeedAsync();
        var reviewedAtUtc = DateTimeOffset.UtcNow;
        var submittedAtUtc = reviewedAtUtc.AddMinutes(-1);
        var decision = new HunterReviewDecision
        {
            Id = Guid.NewGuid(),
            HunterSubmissionId = seed.SubmissionId,
            ReviewerUserId = Guid.NewGuid().ToString("D"),
            ResultingStatus = HunterSubmissionStatus.Approved,
            ReviewedAtUtc = reviewedAtUtc
        };

        try
        {
            await MarkPendingReviewAsync(
                seed.SubmissionId,
                submittedAtUtc);

            await using (var db = CreateContext())
            {
                var store = new SqlHunterReviewStore(db);
                var response = await store.TryReviewPendingAsync(decision);

                Assert.NotNull(response);
                Assert.Equal(
                    HunterSubmissionStatus.Approved,
                    response.Status);
            }

            await using var verifyDb = CreateContext();
            var submission = await verifyDb.HunterSubmissions
                .AsNoTracking()
                .SingleAsync(x => x.Id == seed.SubmissionId);
            var reviewCount = await verifyDb.HunterReviewDecisions
                .AsNoTracking()
                .CountAsync(x => x.HunterSubmissionId == seed.SubmissionId);
            var session = await verifyDb.HunterCaptureSessions
                .AsNoTracking()
                .SingleAsync(x => x.Id == seed.SessionId);

            Assert.Equal(HunterSubmissionStatus.Approved, submission.Status);
            Assert.Equal(1, reviewCount);
            Assert.True(session.IsRevoked);

            await using var retryDb = CreateContext();
            var retryStore = new SqlHunterReviewStore(retryDb);
            Assert.Null(await retryStore.TryReviewPendingAsync(decision));
            Assert.Equal(1, await retryDb.HunterReviewDecisions
                .CountAsync(x => x.HunterSubmissionId == seed.SubmissionId));
        }
        finally
        {
            await CleanupAsync(seed.SubmissionId, seed.UserId);
        }
    }

    // =================================================
    // TEST 05 - Review Rollback on Session Revocation Failure
    // =================================================

    [M4SqlIntegrationFact]
    public async Task ReviewRevocationFailure_ShouldRollbackDecisionAndStatus()
    {
        var seed = await SeedAsync();
        var reviewedAtUtc = DateTimeOffset.UtcNow;

        try
        {
            await MarkPendingReviewAsync(
                seed.SubmissionId,
                reviewedAtUtc.AddMinutes(-1));

            var interceptor = new FailHunterReviewRevocationInterceptor();
            await using (var db = CreateContext(interceptor))
            {
                var store = new SqlHunterReviewStore(db);
                await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    store.TryReviewPendingAsync(new HunterReviewDecision
                    {
                        Id = Guid.NewGuid(),
                        HunterSubmissionId = seed.SubmissionId,
                        ReviewerUserId = Guid.NewGuid().ToString("D"),
                        ResultingStatus = HunterSubmissionStatus.Approved,
                        ReviewedAtUtc = reviewedAtUtc
                    }));
            }

            Assert.True(interceptor.StatusUpdateObserved);
            Assert.True(interceptor.RevocationBlocked);

            await using var verifyDb = CreateContext();
            Assert.Equal(
                HunterSubmissionStatus.PendingReview,
                await verifyDb.HunterSubmissions
                    .Where(x => x.Id == seed.SubmissionId)
                    .Select(x => x.Status)
                    .SingleAsync());
            Assert.Empty(await verifyDb.HunterReviewDecisions
                .Where(x => x.HunterSubmissionId == seed.SubmissionId)
                .ToArrayAsync());
            Assert.False(await verifyDb.HunterCaptureSessions
                .Where(x => x.Id == seed.SessionId)
                .Select(x => x.IsRevoked)
                .SingleAsync());
        }
        finally
        {
            await CleanupAsync(seed.SubmissionId, seed.UserId);
        }
    }

    // =================================================
    // Failure Injection Interceptor
    // =================================================

    private sealed class FailHunterRevocationInterceptor
        : DbCommandInterceptor
    {
        public bool StatusUpdateObserved { get; private set; }

        public bool RevocationBlocked { get; private set; }

        public override ValueTask<InterceptionResult<int>>
            NonQueryExecutingAsync(
                DbCommand command,
                CommandEventData eventData,
                InterceptionResult<int> result,
                CancellationToken cancellationToken = default)
        {
            var sql = command.CommandText;

            var isUpdate = sql.Contains(
                "UPDATE",
                StringComparison.OrdinalIgnoreCase);

            if (isUpdate &&
                sql.Contains(
                    "Hunter_Submissions",
                    StringComparison.OrdinalIgnoreCase) &&
                sql.Contains(
                    "Status",
                    StringComparison.OrdinalIgnoreCase))
            {
                StatusUpdateObserved = true;
            }

            if (StatusUpdateObserved &&
                isUpdate &&
                sql.Contains(
                    "Hunter_CaptureSessions",
                    StringComparison.OrdinalIgnoreCase) &&
                sql.Contains(
                    "IsRevoked",
                    StringComparison.OrdinalIgnoreCase))
            {
                RevocationBlocked = true;

                throw new InvalidOperationException(
                    "Injected M4 capture-session revocation failure.");
            }

            return new ValueTask<InterceptionResult<int>>(result);
        }
    }

    private sealed class FailHunterReviewRevocationInterceptor
        : DbCommandInterceptor
    {
        public bool StatusUpdateObserved { get; private set; }
        public bool RevocationBlocked { get; private set; }

        public override ValueTask<InterceptionResult<int>>
            NonQueryExecutingAsync(
                DbCommand command,
                CommandEventData eventData,
                InterceptionResult<int> result,
                CancellationToken cancellationToken = default)
        {
            var sql = command.CommandText;
            if (sql.Contains("UPDATE", StringComparison.OrdinalIgnoreCase) &&
                sql.Contains("Hunter_Submissions", StringComparison.OrdinalIgnoreCase) &&
                sql.Contains("Status", StringComparison.OrdinalIgnoreCase))
            {
                StatusUpdateObserved = true;
            }

            if (StatusUpdateObserved &&
                sql.Contains("UPDATE", StringComparison.OrdinalIgnoreCase) &&
                sql.Contains("Hunter_CaptureSessions", StringComparison.OrdinalIgnoreCase) &&
                sql.Contains("IsRevoked", StringComparison.OrdinalIgnoreCase))
            {
                RevocationBlocked = true;
                throw new InvalidOperationException(
                    "Injected M4 review session-revocation failure.");
            }

            return new ValueTask<InterceptionResult<int>>(result);
        }
    }
}
