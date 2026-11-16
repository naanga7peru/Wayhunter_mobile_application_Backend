
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

using Xunit;

namespace Wayhunter.IntegrationTests.Modules.HunterPlaces;

// =====================================================
// M4 SQL Integration Test Attribute
// =====================================================
// Tests are skipped unless an isolated SQL test
// database has been explicitly configured.
//
// This code NEVER creates or applies migrations.
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
            Skip =
                "M4 isolated SQL test database is not configured.";
        }
    }
}

// =====================================================
// M4 SQL Integration Verification Tests
// =====================================================

public sealed class M4SqlIntegrationVerificationTests
{
    private const string AllowedDatabaseName =
        "Wayhunter_M4_IntegrationTest";

    // =================================================
    // Shared Test DbContext Factory
    // =================================================

    private static WayhunterSharedDbContext CreateContext(
        DbCommandInterceptor? interceptor = null)
    {
        var connectionString =
            Environment.GetEnvironmentVariable(
                "WAYHUNTER_M4_TEST_SQL_CONNECTION");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "M4 SQL test connection is missing.");
        }

        var connectionBuilder =
            new SqlConnectionStringBuilder(
                connectionString);

        // Safety guard:
        // Never write test data to a shared,
        // development, or production database.

        if (!string.Equals(
                connectionBuilder.InitialCatalog,
                AllowedDatabaseName,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Only the isolated M4 integration " +
                "test database is allowed.");
        }

        var optionsBuilder =
            new DbContextOptionsBuilder<WayhunterSharedDbContext>()
                .UseSqlServer(connectionString);

        // Optional failure injection for rollback tests.
        if (interceptor is not null)
        {
            optionsBuilder.AddInterceptors(interceptor);
        }

        return new WayhunterSharedDbContext(
            optionsBuilder.Options);
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
        var now = DateTimeOffset.UtcNow;

        // Create a test-only M1 account.
        var user = UserAccount.RegisterPending(
            "M4 SQL Test Hunter",
            $"m4-sql-{Guid.NewGuid():N}@example.invalid",
            "test-only-password-hash-placeholder",
            now);

        user.ActivateAfterVerifiedOtp(now);

        // Create a Draft Hunter submission.
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

        // Create an unused capture session.
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

        db.HunterSubmissions.Add(submission);

        db.HunterCaptureSessions.Add(session);

        await db.SaveChangesAsync();

        return (
            Guid.Parse(user.UserId),
            user.UserId,
            submission.Id,
            session.Id);
    }

    // =================================================
    // Test Data Cleanup
    // =================================================

    private static async Task CleanupAsync(
        Guid submissionId,
        string userId)
    {
        await using var db = CreateContext();

        // Remove only records created by this test.

        await db.HunterCaptureSessions
            .Where(x =>
                x.HunterSubmissionId == submissionId)
            .ExecuteDeleteAsync();

        await db.HunterSubmissions
            .Where(x =>
                x.Id == submissionId)
            .ExecuteDeleteAsync();

        await db.Users
            .Where(x =>
                x.UserId == userId)
            .ExecuteDeleteAsync();
    }

    // =================================================
    // TEST 01
    // Concurrent Capture Session Consumption
    // =================================================

    [M4SqlIntegrationFact]
    public async Task
        ConcurrentConsume_ExactlyOne_ShouldSucceed()
    {
        var seed = await SeedAsync();

        try
        {
            async Task<bool> ConsumeAsync()
            {
                // Each request gets its own DbContext.
                await using var db = CreateContext();

                var store =
                    new SqlHunterCaptureSessionStore(db);

                return await store.TryConsumeAsync(
                    seed.SessionId,
                    seed.HunterId,
                    seed.SubmissionId,
                    DateTimeOffset.UtcNow);
            }

            // Simulate two concurrent requests.
            var results = await Task.WhenAll(
                ConsumeAsync(),
                ConsumeAsync());

            // Exactly one request must succeed.
            Assert.Single(
                results,
                result => result);

            await using var verifyDb = CreateContext();

            var consumed = await verifyDb
                .HunterCaptureSessions
                .AsNoTracking()
                .Where(x =>
                    x.Id == seed.SessionId)
                .Select(x =>
                    x.IsConsumed)
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
    // TEST 02
    // Status Transition + Session Revocation
    // =================================================

    [M4SqlIntegrationFact]
    public async Task
        StatusTransition_ShouldRevokeUnusedSessions()
    {
        var seed = await SeedAsync();

        try
        {
            await using (var db = CreateContext())
            {
                var store =
                    new SqlHunterSubmissionStatusTransitionStore(
                        db);

                var changed = await store
                    .TryTransitionAndRevokeCaptureSessionsAsync(
                        seed.SubmissionId,
                        HunterSubmissionStatus.Draft,
                        HunterSubmissionStatus.PendingReview);

                Assert.True(changed);
            }

            // Verify changes using a fresh DbContext.
            await using var verifyDb = CreateContext();

            var status = await verifyDb
                .HunterSubmissions
                .AsNoTracking()
                .Where(x =>
                    x.Id == seed.SubmissionId)
                .Select(x =>
                    x.Status)
                .SingleAsync();

            var revoked = await verifyDb
                .HunterCaptureSessions
                .AsNoTracking()
                .Where(x =>
                    x.Id == seed.SessionId)
                .Select(x =>
                    x.IsRevoked)
                .SingleAsync();

            Assert.Equal(
                HunterSubmissionStatus.PendingReview,
                status);

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
    // TEST 03
    // Transaction Rollback on Revocation Failure
    // =================================================

    [M4SqlIntegrationFact]
    public async Task
        RevocationFailure_ShouldRollbackStatusTransition()
    {
        var seed = await SeedAsync();

        try
        {
            var interceptor =
                new FailHunterRevocationInterceptor();

            await using (var db = CreateContext(interceptor))
            {
                var store =
                    new SqlHunterSubmissionStatusTransitionStore(
                        db);

                // The interceptor throws when the
                // session revocation SQL command runs.

                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => store
                        .TryTransitionAndRevokeCaptureSessionsAsync(
                            seed.SubmissionId,
                            HunterSubmissionStatus.Draft,
                            HunterSubmissionStatus.PendingReview));
            }

            // The status UPDATE must have been reached.
            Assert.True(
                interceptor.StatusUpdateObserved);

            // The revocation command must have failed.
            Assert.True(
                interceptor.RevocationBlocked);

            // Check committed database state
            // using a fresh DbContext.
            await using var verifyDb = CreateContext();

            var submission = await verifyDb
                .HunterSubmissions
                .AsNoTracking()
                .SingleAsync(x =>
                    x.Id == seed.SubmissionId);

            var session = await verifyDb
                .HunterCaptureSessions
                .AsNoTracking()
                .SingleAsync(x =>
                    x.Id == seed.SessionId);

            // The status must be rolled back to Draft.
            Assert.Equal(
                HunterSubmissionStatus.Draft,
                submission.Status);

            // The session must remain unchanged.
            Assert.False(
                session.IsRevoked);

            Assert.False(
                session.IsConsumed);
        }
        finally
        {
            await CleanupAsync(
                seed.SubmissionId,
                seed.UserId);
        }
    }

    // =================================================
    // Failure Injection Interceptor
    // =================================================
    // Blocks the revocation UPDATE after the
    // submission status UPDATE has executed.
    //
    // Used only by Test 03.
    // =================================================

    private sealed class FailHunterRevocationInterceptor
        : DbCommandInterceptor
    {
        public bool StatusUpdateObserved
        {
            get;
            private set;
        }

        public bool RevocationBlocked
        {
            get;
            private set;
        }

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

            // Observe submission status UPDATE.
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

            // Inject failure when the capture-session
            // revocation UPDATE is about to execute.
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
                    "Injected M4 capture-session " +
                    "revocation failure.");
            }

            return new ValueTask<InterceptionResult<int>>(
                result);
        }
    }
}
